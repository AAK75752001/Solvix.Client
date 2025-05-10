using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.MVVM.Views;
using Solvix.Client.Resources.Themes;

namespace Solvix.Client
{
    public partial class App : Application
    {
        private readonly IAuthService _authService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<App> _logger;
        private readonly IThemeService _themeService;

        public App(
            IAuthService authService,
            IServiceProvider serviceProvider,
            IThemeService themeService,
            ILogger<App> logger)
        {
            InitializeComponent();

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));


            _themeService.LoadSavedTheme();

            SetInitialPage();
        }

        protected override void OnStart()
        {
            base.OnStart();
            _logger.LogInformation("App OnStart entered.");
        }

        private void SetInitialPage()
        {
            try
            {
                _logger.LogInformation("SetInitialPage logic running...");
                bool isLoggedIn = _authService.IsLoggedIn();
                _logger.LogInformation("User logged in status check: {IsLoggedIn}", isLoggedIn);

                if (isLoggedIn)
                {
                    _logger.LogInformation("User is logged in. Setting MainPage to AppShell.");
                    MainPage = new AppShell();
                }
                else
                {
                    _logger.LogInformation("User is not logged in. Setting MainPage to LoginPage within NavigationPage.");
                    var loginPage = _serviceProvider.GetService<LoginPage>();
                    if (loginPage != null)
                    {
                        var navPage = new NavigationPage(loginPage) { BarBackgroundColor = Colors.Transparent };
                        NavigationPage.SetHasNavigationBar(navPage, false);
                        MainPage = navPage;
                    }
                    else
                    {
                        _logger.LogError("Failed to resolve LoginPage.");
                        MainPage = new ContentPage { Content = new Label { Text = "Error loading login page." } };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in SetInitialPage.");
                MainPage = new ContentPage { Content = new Label { Text = $"Critical Error: {ex.Message}" } };
            }
        }

        private void ApplySolvixTheme()
        {
            try
            {
                _logger.LogDebug("Applying Solvix theme...");
                var currentDictionaries = Application.Current?.Resources?.MergedDictionaries;
                if (currentDictionaries == null) return;

                var existingThemes = currentDictionaries.OfType<ResourceDictionary>()
                                                     .Where(d => d is LightThemeResources || d is DarkThemeResources || d is SolvixThemeResources)
                                                     .ToList();
                foreach (var theme in existingThemes) { currentDictionaries.Remove(theme); }

                currentDictionaries.Add(new SolvixThemeResources());
                _logger.LogDebug("Applied SolvixThemeResources successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying Solvix theme in App constructor.");
            }
        }

        protected override void OnSleep() { _logger.LogInformation("App entering sleep state."); base.OnSleep(); }

        protected override void OnResume() { _logger.LogInformation("App resuming from sleep state."); base.OnResume(); }
    }
}