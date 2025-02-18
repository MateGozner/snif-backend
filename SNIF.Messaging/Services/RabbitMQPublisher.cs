using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using SNIF.Core.Contracts;
using SNIF.Core.Entities;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Messaging.Configuration;

using System.Text;

namespace SNIF.Messaging.Services
{
    public class RabbitMQPublisher : IMessagePublisher, IDisposable
    {
        private readonly RabbitMQConfig _config;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQPublisher> _logger;
        private const string WATCH_LIST_EXCHANGE = "pet.watchlist";
        private const string MATCH_EXCHANGE = "pet.matches";

        public RabbitMQPublisher(IOptions<RabbitMQConfig> config, ILogger<RabbitMQPublisher> logger)
        {
            _config = config.Value;
            _logger = logger;

            var factory = new ConnectionFactory
            {
                HostName = _config.HostName,
                Port = _config.Port,
                UserName = _config.UserName,
                Password = _config.Password,
                VirtualHost = _config.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            try
            {
                _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}", _config.HostName, _config.Port);
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _logger.LogInformation("Successfully connected to RabbitMQ");

                DeclareExchanges();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ publisher");
                throw;
            }
        }


        private void DeclareExchanges()
        {
            _channel.ExchangeDeclare(
                exchange: WATCH_LIST_EXCHANGE,
                type: "headers",
                durable: true,
                autoDelete: false
            );

            _channel.ExchangeDeclare(
                exchange: MATCH_EXCHANGE,
                type: "topic",
                durable: true,
                autoDelete: false
            );
        }

        public async Task CreateWatchlistQueueForUser(string userId, UserPreferences preferences)
        {
            var queueName = $"watchlist.{userId}";
            var headers = new Dictionary<string, object>
            {
                { "maxDistance", preferences.SearchRadius }
            };


            // Declare user-specific queue
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            // Bind queue to watchlist exchange with headers
            _channel.QueueBind(
                queue: queueName,
                exchange: WATCH_LIST_EXCHANGE,
                routingKey: string.Empty,
                arguments: headers
            );

            // Bind to match exchange for specific notifications
            _channel.QueueBind(
                queue: queueName,
                exchange: MATCH_EXCHANGE,
                routingKey: $"matches.{userId}.#"
            );
        }

        public async Task PublishPetCreatedAsync(Pet pet)
        {
            try
            {
                var petMessage = new
                {
                    pet.Id,
                    pet.Species,
                    pet.Breed,
                    pet.Gender,
                    pet.Purpose,
                    Location = new
                    {
                        pet.Location.Latitude,
                        pet.Location.Longitude
                    },
                    CreatedAt = DateTime.UtcNow
                };

                var headers = new Dictionary<string, object>
                {
                    { "species", pet.Species },
                    { "purposes", string.Join(",", pet.Purpose) }
                };

                var properties = _channel.CreateBasicProperties();
                properties.Headers = headers;
                properties.Persistent = true;
                properties.ContentType = "application/json";

                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(petMessage));

                await Task.Run(() =>
                {
                    _channel.BasicPublish(
                        exchange: WATCH_LIST_EXCHANGE,
                        routingKey: string.Empty,
                        basicProperties: properties,
                        body: body
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish pet created message for pet {PetId}", pet.Id);
                throw;
            }
        }

        public async Task PublishMatchNotificationAsync(string userId, PetMatchNotification notification)
        {
            try
            {
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";

                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(notification));
                var routingKey = $"matches.{userId}.{notification.MatchedPetId}";

                await Task.Run(() =>
                {
                    _channel.BasicPublish(
                        exchange: MATCH_EXCHANGE,
                        routingKey: routingKey,
                        basicProperties: properties,
                        body: body
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish match notification for user {UserId}", userId);
                throw;
            }
        }



        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;

            if (_channel?.IsOpen ?? false)
                _channel?.Close();
            if (_connection?.IsOpen ?? false)
                _connection?.Close();

            _channel?.Dispose();
            _connection?.Dispose();

            _disposed = true;
        }
    }
}