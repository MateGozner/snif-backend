
namespace SNIF.Messaging.Configuration
{
    public class RabbitMQConfig
    {
        public string HostName { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string VirtualHost { get; set; }
        public ExchangeConfig Exchanges { get; set; }
        public int WebSocketPort { get; set; }
    }

    public class ExchangeConfig
    {
        public string Watchlist { get; set; }
        public string Matches { get; set; }
    }

}