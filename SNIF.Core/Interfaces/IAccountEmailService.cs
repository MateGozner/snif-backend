using SNIF.Core.Entities;

namespace SNIF.Core.Interfaces
{
    public interface IAccountEmailService
    {
        Task SendPasswordResetAsync(User user, string token);
        Task SendEmailConfirmationAsync(User user, string token);
    }
}