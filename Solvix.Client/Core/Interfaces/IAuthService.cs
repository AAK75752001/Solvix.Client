using Solvix.Client.Core.Models;

namespace Solvix.Client.Core.Interfaces
{
    public interface IAuthService
    {
        Task<bool> CheckPhoneExists(string phoneNumber);
        Task<UserModel?> LoginAsync(string phoneNumber, string password);
        Task<UserModel?> RegisterAsync(RegisterDto registerDto);
        Task<UserModel?> GetCurrentUserAsync();
        Task<string?> RefreshTokenAsync();
        bool IsLoggedIn();
        Task LogoutAsync();
        Task<string?> GetTokenAsync();
        Task<long> GetUserIdAsync();
    }
}
