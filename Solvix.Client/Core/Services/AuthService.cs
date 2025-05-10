using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;

namespace Solvix.Client.Core.Services
{
    public class AuthService : IAuthService
    {
        private readonly IApiService _apiService;
        private readonly ISecureStorageService _secureStorageService;
        private readonly IToastService _toastService;
        private readonly ITokenManager _tokenManager;

        public AuthService(
            IApiService apiService,
            ISecureStorageService secureStorageService,
            IToastService toastService,
            ITokenManager tokenManager)
        {
            _apiService = apiService;
            _secureStorageService = secureStorageService;
            _toastService = toastService;
            _tokenManager = tokenManager;
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

                if (response != null && !string.IsNullOrEmpty(response.Token))
                {
                    // ذخیره توکن با استفاده از TokenManager
                    await _tokenManager.SaveTokenAsync(response.Token);

                    // ذخیره اطلاعات کاربر در حافظه امن
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

                if (response != null && !string.IsNullOrEmpty(response.Token))
                {
                    // ذخیره توکن با استفاده از TokenManager
                    await _tokenManager.SaveTokenAsync(response.Token);

                    // ذخیره اطلاعات کاربر در حافظه امن
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
                // بررسی اعتبار توکن قبل از درخواست
                if (!await _tokenManager.IsTokenValidAsync())
                {
                    // تلاش برای رفرش توکن
                    string? newToken = await RefreshTokenAsync();
                    if (newToken == null)
                    {
                        // اگر رفرش نشد، لاگین منقضی شده است
                        return null;
                    }
                }

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

                if (response != null && response.token != null)
                {
                    string token = response.token;

                    if (!string.IsNullOrEmpty(token))
                    {
                        await _tokenManager.SaveTokenAsync(token);
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
            try
            {
                var token = _tokenManager.GetTokenAsync().GetAwaiter().GetResult();
                return !string.IsNullOrEmpty(token) && _tokenManager.IsTokenValidAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            await _tokenManager.RemoveTokenAsync();

            await _secureStorageService.RemoveAsync(Constants.StorageKeys.UserId);
            await _secureStorageService.RemoveAsync(Constants.StorageKeys.Username);
            await _secureStorageService.RemoveAsync(Constants.StorageKeys.PhoneNumber);
        }

        public async Task<string?> GetTokenAsync()
        {
            return await _tokenManager.GetTokenAsync();
        }

        public async Task<long> GetUserIdAsync()
        {
            long userId = await _tokenManager.GetUserIdFromTokenAsync();

            if (userId == 0)
            {
                var userIdStr = await _secureStorageService.GetAsync(Constants.StorageKeys.UserId);
                return long.TryParse(userIdStr, out var id) ? id : 0;
            }

            return userId;
        }
    }
}