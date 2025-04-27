using Microsoft.Extensions.Logging;
using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.MVVM.ViewModels;
using Solvix.Client.MVVM.Views;
using Solvix.Client.Resources.Themes;

namespace Solvix.Client
{
    public partial class App : Application
    {
        private readonly IAuthService _authService;
        private readonly ISettingsService _settingsService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<App> _logger;

        public App(
            IAuthService authService,
            ISettingsService settingsService,
            IServiceProvider serviceProvider,
            ILogger<App> logger)
        {
            try
            {
                _logger = logger;
                _logger.LogInformation("Starting app initialization");

                InitializeComponent();

                _authService = authService;
                _settingsService = settingsService;
                _serviceProvider = serviceProvider;

                // Show a loading page immediately
                MainPage = new ContentPage
                {
                    BackgroundColor = Colors.White,
                    Content = new ActivityIndicator
                    {
                        IsRunning = true,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        Color = Colors.Purple
                    }
                };

                // Load theme and proceed with initialization
                ApplyTheme();
                SetInitialPage();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Critical error in App constructor");

                // Fallback UI in case of critical error
                MainPage = new ContentPage
                {
                    BackgroundColor = Colors.White,
                    Content = new VerticalStackLayout
                    {
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new Label
                            {
                                Text = "Failed to start application",
                                FontSize = 18,
                                HorizontalOptions = LayoutOptions.Center
                            },
                            new Label
                            {
                                Text = ex.Message,
                                FontSize = 14,
                                HorizontalOptions = LayoutOptions.Center,
                                Margin = new Thickness(20, 10, 20, 0)
                            }
                        }
                    }
                };
            }
        }

        private void SetInitialPage()
        {
            try
            {
                _logger.LogInformation("Setting initial page");
                bool isLoggedIn = false;

                try
                {
                    isLoggedIn = _authService.IsLoggedIn();
                    _logger.LogInformation("User logged in: {IsLoggedIn}", isLoggedIn);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking login status");
                    isLoggedIn = false;
                }

                if (isLoggedIn)
                {
                    _logger.LogInformation("User is logged in, navigating to main app");
                    MainPage = new AppShell();
                }
                else
                {
                    _logger.LogInformation("User is not logged in, navigating to login");

                    try
                    {
                        var loginPage = _serviceProvider.GetService<LoginPage>();
                        if (loginPage != null)
                        {
                            MainPage = new NavigationPage(loginPage);
                        }
                        else
                        {
                            _logger.LogError("Failed to resolve LoginPage from service provider");

                            // Create a simple error page instead of trying direct instantiation
                            MainPage = new ContentPage
                            {
                                Content = new Label
                                {
                                    Text = "Error: Could not create login page",
                                    HorizontalOptions = LayoutOptions.Center,
                                    VerticalOptions = LayoutOptions.Center
                                }
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating login page");

                        // Last resort - create a simple error page
                        MainPage = new ContentPage
                        {
                            Content = new Label
                            {
                                Text = "Error loading login page: " + ex.Message,
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.Center
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error setting initial page");

                // Fallback UI
                MainPage = new ContentPage
                {
                    Content = new Label
                    {
                        Text = "Error starting application: " + ex.Message,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    }
                };
            }
        }

        protected override void OnStart()
        {
            _logger.LogInformation("App OnStart");
            base.OnStart();
        }

        protected override void OnSleep()
        {
            _logger.LogInformation("App OnSleep");
            base.OnSleep();
        }

        protected override void OnResume()
        {
            _logger.LogInformation("App OnResume");
            base.OnResume();
        }

        private void ApplyTheme()
        {
            try
            {
                var theme = _settingsService.GetTheme();
                _logger.LogInformation("Applying theme: {Theme}", theme);

                // Get the currently merged theme dictionaries (if any)
                var themeDict = Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d is LightThemeResources || d is DarkThemeResources);

                // Remove old theme dictionary if found
                if (themeDict != null)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(themeDict);
                }

                // Add new theme dictionary
                if (string.IsNullOrEmpty(theme) || theme == Constants.Themes.Light)
                {
                    _logger.LogInformation("Adding Light theme resources");
                    Application.Current.Resources.MergedDictionaries.Add(new LightThemeResources());
                }
                else
                {
                    _logger.LogInformation("Adding Dark theme resources");
                    Application.Current.Resources.MergedDictionaries.Add(new DarkThemeResources());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying theme");

                // Fallback to light theme in case of error
                try
                {
                    Application.Current.Resources.MergedDictionaries.Add(new LightThemeResources());
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Critical error applying fallback theme");
                }
            }
        }
    }
}