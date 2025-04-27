using Solvix.Client.Core.Interfaces;

namespace Solvix.Client.Core.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ISecureStorageService _secureStorageService;

        public SettingsService(ISecureStorageService secureStorageService)
        {
            _secureStorageService = secureStorageService;
        }

        public string GetTheme()
        {
            var theme = _secureStorageService.GetAsync(Constants.StorageKeys.Theme).Result;
            return string.IsNullOrEmpty(theme) ? Constants.Themes.Light : theme;
        }

        public async Task SetThemeAsync(string theme)
        {
            await _secureStorageService.SaveAsync(Constants.StorageKeys.Theme, theme);
        }
    }
}
