using SNIF.Core.Entities;
using System.Security.Claims;

namespace SNIF.Core.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(User user);
        ClaimsPrincipal? ValidateToken(string token);
    }
}