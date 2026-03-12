namespace SNIF.Core.Interfaces
{
    public interface IPushNotificationService
    {
        Task RegisterDeviceAsync(string userId, string token, string platform);
        Task UnregisterDeviceAsync(string userId, string token);
        Task SendPushAsync(string userId, string title, string body, Dictionary<string, string>? data = null);
    }
}
