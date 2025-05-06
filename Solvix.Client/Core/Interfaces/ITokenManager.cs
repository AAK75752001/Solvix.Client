
namespace Solvix.Client.Core.Interfaces
{
    public interface ITokenManager
    {
        Task<string?> GetTokenAsync();
        Task SaveTokenAsync(string token);
        Task RemoveTokenAsync();
        Task<bool> IsTokenValidAsync();
        Task<Dictionary<string, string>?> GetTokenClaimsAsync();
        Task<long> GetUserIdFromTokenAsync();
        Task<string> GetUsernameFromTokenAsync();
        Task<DateTime> GetTokenExpiryTimeAsync();
    }
}
