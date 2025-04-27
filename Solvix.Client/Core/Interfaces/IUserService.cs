using Solvix.Client.Core.Models;

namespace Solvix.Client.Core.Interfaces
{
    public interface IUserService
    {
        Task<List<UserModel>> SearchUsersAsync(string query);
        Task<UserModel?> GetUserAsync(long userId);
        Task<List<UserModel>> GetOnlineUsersAsync();
    }
}
