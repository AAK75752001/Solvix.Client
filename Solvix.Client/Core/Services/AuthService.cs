using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;

namespace Solvix.Client.Core.Services
{
    public class AuthService : IAuthService
    {
        private readonly IApiService _apiService;
        private readonly ISecureStorageService _secureStorageService;
        private readonly IToastService _toastService;

        public AuthService(IApiService apiService, ISecureStorageService secureStorageService, IToastService toastService)
        {
            _apiService = apiService;
            _secureStorageService = secureStorageService;
            _toastService = toastService;
        }

        public async Task<bool> CheckPhoneExists(string phoneNumber)
        {
            try
            {
                var endpoint = $"{Constants.Endpoints.CheckPhone}/{phoneNumber}";
                var response = await _apiService.GetAsync<PhoneCheckResponse>(endpoint, false);
                return response?.Exists ?? false;
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Error checking phone: {ex.Message}", ToastType.Error);
                return false;
            }
        }

        public async Task<UserModel?> LoginAsync(string phoneNumber, string password)
        {
            try
            {
                var loginDto = new LoginDto
                {
                    PhoneNumber = phoneNumber,
                    Password = password
                };

                var response = await _apiService.PostAsync<UserModel>(Constants.Endpoints.Login, loginDto, false);

                if (response != null)
                {
                    // Save user data to secure storage
                    await _secureStorageService.SaveAsync(Constants.StorageKeys.AuthToken, response.Token);
                    await _secureStorageService.SaveAsync(Constants.StorageKeys.UserId, response.Id.ToString());
                    await _secureStorageService.SaveAsync(Constants.StorageKeys.Username, response.Username);
                    await _secureStorageService.SaveAsync(Constants.StorageKeys.PhoneNumber, response.PhoneNumber);

                    return response;
                }

                return null;
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Login failed: {ex.Message}", ToastType.Error);
                return null;
            }
        }

        public async Task<UserModel?> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                var response = await _apiService.PostAsync<UserModel>(Constants.Endpoints.Register, registerDto, false);

                if (response != null)
                {
                    // Save user data to secure storage
                    await _secureStorageService.SaveAsync(Constants.StorageKeys.AuthToken, response.Token);
                    await _secureStorageService.SaveAsync(Constants.StorageKeys.UserId, response.Id.ToString());
                    await _secureStorageService.SaveAsync(Constants.StorageKeys.Username, response.Username);
                    await _secureStorageService.SaveAsync(Constants.StorageKeys.PhoneNumber, response.PhoneNumber);

                    return response;
                }

                return null;
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Registration failed: {ex.Message}", ToastType.Error);
                return null;
            }
        }

        public async Task<UserModel?> GetCurrentUserAsync()
        {
            try
            {
                var response = await _apiService.GetAsync<UserModel>(Constants.Endpoints.CurrentUser);
                return response;
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Error getting user: {ex.Message}", ToastType.Error);
                return null;
            }
        }

        public async Task<string?> RefreshTokenAsync()
        {
            try
            {
                var response = await _apiService.GetAsync<dynamic>(Constants.Endpoints.RefreshToken);

                if (response != null)
                {
                    string token = response.token;

                    if (!string.IsNullOrEmpty(token))
                    {
                        await _secureStorageService.SaveAsync(Constants.StorageKeys.AuthToken, token);
                        return token;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Token refresh failed: {ex.Message}", ToastType.Error);
                return null;
            }
        }

        public bool IsLoggedIn()
        {
            var token = _secureStorageService.GetAsync(Constants.StorageKeys.AuthToken).Result;
            return !string.IsNullOrEmpty(token);
        }

        public async Task LogoutAsync()
        {
            await _secureStorageService.RemoveAsync(Constants.StorageKeys.AuthToken);
            await _secureStorageService.RemoveAsync(Constants.StorageKeys.UserId);
            await _secureStorageService.RemoveAsync(Constants.StorageKeys.Username);
            await _secureStorageService.RemoveAsync(Constants.StorageKeys.PhoneNumber);
        }

        public async Task<string?> GetTokenAsync()
        {
            return await _secureStorageService.GetAsync(Constants.StorageKeys.AuthToken);
        }

        public async Task<long> GetUserIdAsync()
        {
            var userId = await _secureStorageService.GetAsync(Constants.StorageKeys.UserId);
            return long.TryParse(userId, out var id) ? id : 0;
        }
    }
}
