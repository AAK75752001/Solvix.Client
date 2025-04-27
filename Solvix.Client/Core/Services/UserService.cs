using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;

namespace Solvix.Client.Core.Services
{
    public class UserService : IUserService
    {
        private readonly IApiService _apiService;
        private readonly IToastService _toastService;

        public UserService(IApiService apiService, IToastService toastService)
        {
            _apiService = apiService;
            _toastService = toastService;
        }

        public async Task<List<UserModel>> SearchUsersAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return new List<UserModel>();

                var queryParams = new Dictionary<string, string>
        {
            { "query", query }
        };

                var response = await _apiService.GetAsync<List<UserModel>>(Constants.Endpoints.SearchUsers, queryParams);

                // اگر نتیجه خالی بود، سعی کنیم جستجو را به صورت محلی انجام دهیم
                if ((response == null || response.Count == 0) && query.Length >= 2)
                {
                    // دریافت همه کاربران آنلاین
                    var allUsers = await GetOnlineUsersAsync();

                    // فیلتر کردن کاربران بر اساس جستجو (صرف نظر از حروف بزرگ و کوچک)
                    var searchQuery = query.ToLowerInvariant();
                    var filteredUsers = allUsers.Where(u =>
                        (u.FirstName != null && u.FirstName.ToLowerInvariant().Contains(searchQuery)) ||
                        (u.LastName != null && u.LastName.ToLowerInvariant().Contains(searchQuery)) ||
                        (u.PhoneNumber != null && u.PhoneNumber.Contains(query)) ||
                        (u.Username != null && u.Username.ToLowerInvariant().Contains(searchQuery))
                    ).ToList();

                    return filteredUsers;
                }

                return response ?? new List<UserModel>();
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Search failed: {ex.Message}", ToastType.Error);
                return new List<UserModel>();
            }
        }

        public async Task<UserModel?> GetUserAsync(long userId)
        {
            try
            {
                var endpoint = $"{Constants.Endpoints.GetUser}/{userId}";
                return await _apiService.GetAsync<UserModel>(endpoint);
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Failed to get user: {ex.Message}", ToastType.Error);
                return null;
            }
        }

        public async Task<List<UserModel>> GetOnlineUsersAsync()
        {
            try
            {
                var response = await _apiService.GetAsync<List<UserModel>>(Constants.Endpoints.GetOnlineUsers);
                return response ?? new List<UserModel>();
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Failed to get online users: {ex.Message}", ToastType.Error);
                return new List<UserModel>();
            }
        }
    }
}
