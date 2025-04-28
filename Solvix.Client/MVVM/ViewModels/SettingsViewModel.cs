using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using Solvix.Client.MVVM.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Solvix.Client.Resources.Themes;
using Microsoft.Extensions.Logging;

namespace Solvix.Client.MVVM.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private readonly ISettingsService _settingsService;
        private readonly IToastService _toastService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SettingsViewModel> _logger;

        private UserModel _currentUser;
        private string _selectedTheme;
        private bool _isLoading;

        public UserModel CurrentUser
        {
            get => _currentUser;
            set
            {
                if (_currentUser != value)
                {
                    _currentUser = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<string> AvailableThemes { get; } = new List<string>
        {
            Constants.Themes.Light,
            Constants.Themes.Dark,
            Constants.Themes.System
        };

        public ICommand BackCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand EditProfileCommand { get; }
        public ICommand AboutCommand { get; }
        public ICommand SelectThemeCommand { get; }

        public SettingsViewModel(
    IAuthService authService,
    ISettingsService settingsService,
    IToastService toastService,
    IServiceProvider serviceProvider,
    ILogger<SettingsViewModel> logger)
        {
            _authService = authService;
            _settingsService = settingsService;
            _toastService = toastService;
            _serviceProvider = serviceProvider;
            _logger = logger;

            BackCommand = new Command(async () => await GoBackAsync());
            LogoutCommand = new Command(async () => await LogoutAsync());
            EditProfileCommand = new Command(async () => await EditProfileAsync());
            AboutCommand = new Command(async () => await ShowAboutAsync());
            SelectThemeCommand = new Command<string>(async (theme) => await SetThemeAsync(theme));

            // Set default values for properties to avoid null references
            _currentUser = new UserModel();
            _selectedTheme = Constants.Themes.Light;

            // Iniciar carga en segundo plano
            Task.Run(() => LoadSettingsAsync());
        }

        private async Task LoadSettingsAsync()
        {
            if (IsLoading) return;

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => {
                    IsLoading = true;
                    _logger.LogInformation("Loading settings and user info");
                });

                // Cargar tema primero (operación rápida)
                var theme = _settingsService.GetTheme();

                await MainThread.InvokeOnMainThreadAsync(() => {
                    SelectedTheme = theme;
                    _logger.LogInformation("Theme loaded: {Theme}", SelectedTheme);
                });

                // Cargar información del usuario en segundo plano
                try
                {
                    var user = await _authService.GetCurrentUserAsync();

                    await MainThread.InvokeOnMainThreadAsync(() => {
                        if (user != null)
                        {
                            CurrentUser = user;
                            _logger.LogInformation("User info loaded: {Username}", user.Username);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to load user info");
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading user data");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings");
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => {
                    IsLoading = false;
                });
            }
        }

        private async Task SetThemeAsync(string theme)
        {
            if (string.IsNullOrEmpty(theme) || theme == SelectedTheme)
                return;

            try
            {
                _logger.LogInformation("Setting theme to: {Theme}", theme);
                SelectedTheme = theme;

                await _settingsService.SetThemeAsync(theme);

                // Apply theme change with animation
                await ApplyThemeChangeAsync(theme);

                await _toastService.ShowToastAsync($"{theme} theme applied", ToastType.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying theme: {Theme}", theme);
                await _toastService.ShowToastAsync($"Error applying theme: {ex.Message}", ToastType.Error);
            }
        }

        private async Task ApplyThemeChangeAsync(string theme)
        {
            try
            {
                _logger.LogInformation("Applying theme change to: {Theme}", theme);

                if (Application.Current?.Resources?.MergedDictionaries != null)
                {
                    // Find and remove the current theme dictionary
                    var themeDict = Application.Current.Resources.MergedDictionaries
                        .FirstOrDefault(d => d is LightThemeResources || d is DarkThemeResources);

                    if (themeDict != null)
                    {
                        Application.Current.Resources.MergedDictionaries.Remove(themeDict);
                    }

                    // Add the new theme dictionary with a short delay for visual effect
                    await Task.Delay(100);

                    if (theme == Constants.Themes.Dark)
                    {
                        Application.Current.Resources.MergedDictionaries.Add(new DarkThemeResources());
                        _logger.LogInformation("Dark theme applied");
                    }
                    else
                    {
                        Application.Current.Resources.MergedDictionaries.Add(new LightThemeResources());
                        _logger.LogInformation("Light theme applied");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ApplyThemeChangeAsync for theme: {Theme}", theme);
                throw;
            }
        }

        private async Task GoBackAsync()
        {
            try
            {
                _logger.LogInformation("Navigating back from settings");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating back from settings");
                // En caso de error, intentar cerrar la página manualmente
                try
                {
                    await Shell.Current.Navigation.PopAsync();
                }
                catch
                {
                    // Si también falla, no hay más opciones
                }
            }
        }

        private async Task LogoutAsync()
        {
            try
            {
                _logger.LogInformation("Logout requested");

                bool confirm = await Shell.Current.CurrentPage.DisplayAlert(
                    "Logout",
                    "Are you sure you want to logout?",
                    "Yes",
                    "No");

                if (confirm)
                {
                    _logger.LogInformation("Logout confirmed");
                    IsLoading = true;

                    // Small delay for better UX
                    await Task.Delay(300);

                    await _authService.LogoutAsync();
                    await _toastService.ShowToastAsync("You have been logged out", ToastType.Success);

                    // Use service provider to resolve LoginPage with its dependencies
                    var loginPage = _serviceProvider.GetService<LoginPage>();
                    Application.Current.MainPage = new NavigationPage(loginPage);

                    _logger.LogInformation("Logout complete");
                }
                else
                {
                    _logger.LogInformation("Logout cancelled");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed");
                await _toastService.ShowToastAsync($"Logout failed: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task EditProfileAsync()
        {
            _logger.LogInformation("Edit profile requested");
            await _toastService.ShowToastAsync("Profile editing will be available soon", ToastType.Info);
        }

        private async Task ShowAboutAsync()
        {
            _logger.LogInformation("About dialog requested");

            await Shell.Current.CurrentPage.DisplayAlert(
                "About Solvix Chat",
                "Version 1.0.0\n\nSolvix Chat is a modern, secure messaging application developed with .NET MAUI.\n\nConnect with friends and colleagues with real-time messaging, online status, and more features coming soon.\n\n© 2025 Solvix Team",
                "OK");
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}