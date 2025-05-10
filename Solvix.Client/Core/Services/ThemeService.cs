using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Resources.Themes;

namespace Solvix.Client.Core.Services
{
    public class ThemeService : IThemeService
    {
        private readonly ISecureStorageService _secureStorageService;
        private readonly ILogger<ThemeService> _logger;
        private const string ThemeKey = "AppTheme";
        private const string NeonGlowKey = "NeonGlow";

        public ThemeService(ISecureStorageService secureStorageService, ILogger<ThemeService> logger)
        {
            _secureStorageService = secureStorageService;
            _logger = logger;
        }

        public void SetTheme(AppTheme theme)
        {
            try
            {
                Application.Current.UserAppTheme = theme;

                var currentDictionaries = Application.Current.Resources.MergedDictionaries;
                var existingTheme = currentDictionaries.FirstOrDefault(d => d is LightThemeResources || d is DarkThemeResources);

                if (existingTheme != null)
                {
                    currentDictionaries.Remove(existingTheme);
                }

                // اضافه کردن تم جدید
                if (theme == AppTheme.Dark)
                {
                    currentDictionaries.Add(new DarkThemeResources());
                }
                else
                {
                    currentDictionaries.Add(new LightThemeResources());
                }

                // ذخیره تم انتخاب شده
                _secureStorageService.SaveAsync(ThemeKey, theme.ToString()).ConfigureAwait(false);
                _logger.LogInformation("Theme changed to: {Theme}", theme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting theme to {Theme}", theme);
            }
        }

        public AppTheme GetCurrentTheme()
        {
            return Application.Current.UserAppTheme;
        }

        public async void LoadSavedTheme()
        {
            try
            {
                var savedTheme = await _secureStorageService.GetAsync(ThemeKey);

                if (string.IsNullOrEmpty(savedTheme))
                {
                    // اگر تم ذخیره شده نبود، از تم سیستم استفاده کن
                    SetTheme(AppTheme.Unspecified);
                    return;
                }

                if (Enum.TryParse<AppTheme>(savedTheme, out var theme))
                {
                    SetTheme(theme);
                }
                else
                {
                    SetTheme(AppTheme.Unspecified);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading saved theme");
                SetTheme(AppTheme.Unspecified);
            }
        }

        public void ApplyNeonGlow(bool enable)
        {
            try
            {
                _secureStorageService.SaveAsync(NeonGlowKey, enable.ToString()).ConfigureAwait(false);

                if (enable)
                {
                    // اضافه کردن افکت‌های نئونی
                    var resources = Application.Current.Resources;
                    resources["NeonGlowEnabled"] = true;
                    resources["NeonGlowIntensity"] = 0.8;
                }
                else
                {
                    var resources = Application.Current.Resources;
                    resources["NeonGlowEnabled"] = false;
                    resources["NeonGlowIntensity"] = 0.0;
                }

                _logger.LogInformation("Neon glow effect set to: {Enable}", enable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting neon glow to {Enable}", enable);
            }
        }
    }
}