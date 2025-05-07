using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using Solvix.Client.MVVM.Views;
using System.Reflection;

namespace Solvix.Client.MVVM.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        #region Services and Logger
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        private readonly ISecureStorageService _secureStorageService;
        private readonly ILogger<SettingsViewModel> _logger;
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private UserModel? _currentUser;

        [ObservableProperty]
        private bool _isNotificationsEnabled = true;

        [ObservableProperty]
        private bool _isTwoFactorEnabled = false;

        [ObservableProperty]
        private bool _lightThemeSelected;

        [ObservableProperty]
        private bool _darkThemeSelected;

        [ObservableProperty]
        private bool _systemThemeSelected = true;

        [ObservableProperty]
        private string _appVersionInfo = string.Empty;
        #endregion

        public SettingsViewModel(
            IToastService toastService,
            IAuthService authService,
            ISecureStorageService secureStorageService,
            ILogger<SettingsViewModel> logger)
        {
            _toastService = toastService;
            _authService = authService;
            _secureStorageService = secureStorageService;
            _logger = logger;

            // در زمان شروع، اطلاعات نسخه برنامه را دریافت می‌کنیم
            SetAppVersionInfo();

            // دریافت اطلاعات کاربر فعلی
            LoadUserInfoAsync().ConfigureAwait(false);

            // خواندن تنظیمات از حافظه
            LoadSettings();
        }

        #region Private Methods
        private void SetAppVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                AppVersionInfo = $"نسخه {version?.Major}.{version?.Minor}.{version?.Build} - سالویکس مسنجر";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت اطلاعات نسخه برنامه");
                AppVersionInfo = "سالویکس مسنجر";
            }
        }

        private async Task LoadUserInfoAsync()
        {
            try
            {
                var user = await _authService.GetCurrentUserAsync();
                if (user != null)
                {
                    CurrentUser = user;
                    _logger.LogInformation("اطلاعات کاربر با موفقیت بارگذاری شد: {Username}", user.Username);
                }
                else
                {
                    _logger.LogWarning("کاربر فعلی یافت نشد یا احراز هویت نشده است");
                    CurrentUser = new UserModel
                    {
                        FirstName = "کاربر",
                        LastName = "ناشناس",
                        PhoneNumber = "..."
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری اطلاعات کاربر");
                await _toastService.ShowToastAsync("خطا در دریافت اطلاعات حساب کاربری", ToastType.Error);
            }
        }

        private async void LoadSettings()
        {
            try
            {
                // بارگذاری تنظیمات اعلان‌ها
                var notificationsSetting = await _secureStorageService.GetAsync(Constants.StorageKeys.Notifications);
                IsNotificationsEnabled = string.IsNullOrEmpty(notificationsSetting) || notificationsSetting == "true";

                // بارگذاری تنظیمات احراز هویت دو مرحله‌ای
                var twoFactorSetting = await _secureStorageService.GetAsync(Constants.StorageKeys.TwoFactorAuth);
                IsTwoFactorEnabled = twoFactorSetting == "true";

                // بارگذاری تنظیمات تم
                var themeSetting = await _secureStorageService.GetAsync(Constants.StorageKeys.Theme);

                if (string.IsNullOrEmpty(themeSetting) || themeSetting == Constants.Themes.System)
                {
                    SystemThemeSelected = true;
                    LightThemeSelected = false;
                    DarkThemeSelected = false;
                }
                else if (themeSetting == Constants.Themes.Light)
                {
                    SystemThemeSelected = false;
                    LightThemeSelected = true;
                    DarkThemeSelected = false;
                }
                else if (themeSetting == Constants.Themes.Dark)
                {
                    SystemThemeSelected = false;
                    LightThemeSelected = false;
                    DarkThemeSelected = true;
                }

                _logger.LogDebug("تنظیمات با موفقیت بارگذاری شدند. تم: {Theme}, اعلان‌ها: {Notifications}, احراز هویت دو مرحله‌ای: {TwoFactor}",
                    themeSetting, IsNotificationsEnabled, IsTwoFactorEnabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری تنظیمات");
            }
        }

        private async Task SaveThemeSettingAsync()
        {
            try
            {
                string themeValue;

                if (SystemThemeSelected)
                {
                    themeValue = Constants.Themes.System;
                }
                else if (LightThemeSelected)
                {
                    themeValue = Constants.Themes.Light;
                }
                else if (DarkThemeSelected)
                {
                    themeValue = Constants.Themes.Dark;
                }
                else
                {
                    // پیش‌فرض اگر هیچکدام انتخاب نشده باشند
                    themeValue = Constants.Themes.System;
                    SystemThemeSelected = true;
                }

                await _secureStorageService.SaveAsync(Constants.StorageKeys.Theme, themeValue);
                _logger.LogInformation("تنظیمات تم ذخیره شد: {Theme}", themeValue);

                // تغییر تم برنامه
                ApplyAppTheme(themeValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ذخیره تنظیمات تم");
                await _toastService.ShowToastAsync("خطا در ذخیره تنظیمات تم", ToastType.Error);
            }
        }

        private void ApplyAppTheme(string themeName)
        {
            try
            {
                if (Application.Current == null) return;

                // اعمال تم متناسب با مقدار انتخاب شده
                Application.Current.UserAppTheme = themeName switch
                {
                    Constants.Themes.Light => AppTheme.Light,
                    Constants.Themes.Dark => AppTheme.Dark,
                    _ => AppTheme.Unspecified // برای System
                };

                _logger.LogInformation("تم برنامه به {Theme} تغییر کرد", themeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تغییر تم برنامه");
            }
        }
        #endregion

        #region Command Methods
        [RelayCommand]
        private async Task EditProfileAsync()
        {
            _logger.LogInformation("درخواست ویرایش پروفایل");
            await _toastService.ShowToastAsync("به زودی امکان ویرایش پروفایل اضافه می‌شود.", ToastType.Info);
        }

        [RelayCommand]
        private async Task ChangePhoneAsync()
        {
            _logger.LogInformation("درخواست تغییر شماره تلفن");
            await _toastService.ShowToastAsync("به زودی امکان تغییر شماره تلفن اضافه می‌شود.", ToastType.Info);
        }

        [RelayCommand]
        private async Task ChangePasswordAsync()
        {
            _logger.LogInformation("درخواست تغییر رمز عبور");
            await _toastService.ShowToastAsync("به زودی امکان تغییر رمز عبور اضافه می‌شود.", ToastType.Info);
        }

        [RelayCommand]
        private async Task ViewSessionsAsync()
        {
            _logger.LogInformation("درخواست مشاهده جلسات فعال");
            await _toastService.ShowToastAsync("به زودی امکان مشاهده جلسات فعال اضافه می‌شود.", ToastType.Info);
        }

        [RelayCommand]
        private async Task SoundSettingsAsync()
        {
            _logger.LogInformation("درخواست تنظیمات صدا");
            await _toastService.ShowToastAsync("به زودی امکان تنظیم صدا و ویبره اضافه می‌شود.", ToastType.Info);
        }

        [RelayCommand]
        private async Task FontSizeSettingsAsync()
        {
            _logger.LogInformation("درخواست تنظیمات اندازه متن");
            await _toastService.ShowToastAsync("به زودی امکان تغییر اندازه متن اضافه می‌شود.", ToastType.Info);
        }

        [RelayCommand]
        private async Task ChatBackgroundAsync()
        {
            _logger.LogInformation("درخواست تغییر پس‌زمینه چت");
            await _toastService.ShowToastAsync("به زودی امکان تغییر پس‌زمینه چت اضافه می‌شود.", ToastType.Info);
        }

        [RelayCommand]
        private async Task LastSeenSettingsAsync()
        {
            _logger.LogInformation("درخواست تنظیمات آخرین بازدید");
            await _toastService.ShowToastAsync("به زودی امکان تنظیم نمایش آخرین بازدید اضافه می‌شود.", ToastType.Info);
        }

        [RelayCommand]
        private async Task BlockedUsersAsync()
        {
            _logger.LogInformation("درخواست مشاهده کاربران مسدود شده");
            await _toastService.ShowToastAsync("به زودی امکان مدیریت کاربران مسدود شده اضافه می‌شود.", ToastType.Info);
        }

        [RelayCommand]
        private async Task AboutAsync()
        {
            _logger.LogInformation("درخواست اطلاعات درباره برنامه");
            await _toastService.ShowToastAsync("سالویکس مسنجر\nیک پیام‌رسان امن", ToastType.Info);
        }

        [RelayCommand]
        private async Task HelpAsync()
        {
            _logger.LogInformation("درخواست راهنما و پشتیبانی");
            await _toastService.ShowToastAsync("به زودی بخش راهنما و پشتیبانی اضافه می‌شود.", ToastType.Info);
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            _logger.LogInformation("درخواست خروج از حساب کاربری");

            try
            {
                // پرسیدن تأیید از کاربر
                bool confirm = await Application.Current!.MainPage!.DisplayAlert(
                    "خروج از حساب",
                    "آیا مطمئن هستید که می‌خواهید از حساب کاربری خارج شوید؟",
                    "بله",
                    "خیر");

                if (confirm)
                {
                    _logger.LogInformation("خروج از حساب کاربری تأیید شد");
                    await _authService.LogoutAsync();

                    // انتقال به صفحه ورود
                    Application.Current.MainPage = new NavigationPage(new LoginPage(
                        Application.Current.Handler.MauiContext!.Services.GetService<LoginViewModel>()!));

                    await _toastService.ShowToastAsync("با موفقیت از حساب کاربری خارج شدید.", ToastType.Success);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در خروج از حساب کاربری");
                await _toastService.ShowToastAsync("خطا در خروج از حساب کاربری. لطفاً مجدداً تلاش کنید.", ToastType.Error);
            }
        }
        #endregion

        #region Property Changed Handlers
        partial void OnLightThemeSelectedChanged(bool value)
        {
            if (value)
            {
                _logger.LogInformation("تم روشن انتخاب شد");
                DarkThemeSelected = false;
                SystemThemeSelected = false;
                SaveThemeSettingAsync().ConfigureAwait(false);
            }
        }

        partial void OnDarkThemeSelectedChanged(bool value)
        {
            if (value)
            {
                _logger.LogInformation("تم تاریک انتخاب شد");
                LightThemeSelected = false;
                SystemThemeSelected = false;
                SaveThemeSettingAsync().ConfigureAwait(false);
            }
        }

        partial void OnSystemThemeSelectedChanged(bool value)
        {
            if (value)
            {
                _logger.LogInformation("تم سیستمی انتخاب شد");
                LightThemeSelected = false;
                DarkThemeSelected = false;
                SaveThemeSettingAsync().ConfigureAwait(false);
            }
        }

        partial void OnIsNotificationsEnabledChanged(bool value)
        {
            _logger.LogInformation("تغییر وضعیت اعلان‌ها به: {Value}", value);
            _secureStorageService.SaveAsync(Constants.StorageKeys.Notifications, value.ToString().ToLower())
                .ConfigureAwait(false);
        }

        partial void OnIsTwoFactorEnabledChanged(bool value)
        {
            _logger.LogInformation("تغییر وضعیت احراز هویت دو مرحله‌ای به: {Value}", value);
            _secureStorageService.SaveAsync(Constants.StorageKeys.TwoFactorAuth, value.ToString().ToLower())
                .ConfigureAwait(false);
        }
        #endregion
    }
}