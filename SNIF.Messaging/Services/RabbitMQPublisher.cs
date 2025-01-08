using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using SNIF.Core.Interfaces;
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

                // Declare exchange with detailed logging
                _logger.LogInformation("Declaring exchange '{ExchangeName}' of type 'topic'", _config.ExchangeName);
                _channel.ExchangeDeclare(
                    exchange: _config.ExchangeName,
                    type: "topic",  // Explicitly use string instead of ExchangeType
                    durable: true,
                    autoDelete: false,
                    arguments: null
                );
                _logger.LogInformation("Exchange '{ExchangeName}' declared successfully", _config.ExchangeName);

                // Declare queue with detailed logging
                _logger.LogInformation("Declaring queue '{QueueName}'", _config.QueueName);
                _channel.QueueDeclare(
                    queue: _config.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );
                _logger.LogInformation("Queue '{QueueName}' declared successfully", _config.QueueName);

                // Bind queue to exchange
                _logger.LogInformation("Binding queue '{QueueName}' to exchange '{ExchangeName}' with routing key 'pet.matches.*'",
                    _config.QueueName, _config.ExchangeName);
                _channel.QueueBind(
                    queue: _config.QueueName,
                    exchange: _config.ExchangeName,
                    routingKey: "pet.matches.*"
                );
                _logger.LogInformation("Queue binding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ publisher");
                throw;
            }
        }

        public async Task PublishAsync<T>(string routingKey, T message)
        {
            try
            {
                _logger.LogInformation("Publishing message with routing key '{RoutingKey}' to exchange '{ExchangeName}'",
                    routingKey, _config.ExchangeName);

                var json = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(json);
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";

                await Task.Run(() =>
                {
                    _channel.BasicPublish(
                        exchange: _config.ExchangeName,
                        routingKey: routingKey,
                        basicProperties: properties,
                        body: body
                    );
                });

                _logger.LogInformation("Successfully published message with routing key '{RoutingKey}'", routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message with routing key '{RoutingKey}'", routingKey);
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