using SNIF.Core.Entities;

namespace SNIF.Core.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(User user);
    }
}