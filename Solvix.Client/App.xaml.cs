using Microsoft.Extensions.Logging;
using Solvix.Client;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.MVVM.Views;

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

        try
        {
            _themeService.LoadSavedTheme();
            SetInitialPage();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in App constructor");
            // Fallback UI
            MainPage = new ContentPage
            {
                Content = new Label
                {
                    Text = $"Initialization error: {ex.Message}",
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
        }
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
                    MainPage = new NavigationPage(loginPage);
                }
                else
                {
                    _logger.LogError("Failed to resolve LoginPage.");
                    MainPage = new ContentPage
                    {
                        Content = new Label
                        {
                            Text = "Error loading login page.",
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center
                        }
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in SetInitialPage.");
            MainPage = new ContentPage
            {
                Content = new Label
                {
                    Text = $"Critical Error: {ex.Message}",
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
        }
    }

    protected override void OnStart()
    {
        _logger.LogInformation("Application OnStart");
    }

    protected override void OnSleep()
    {
        _logger.LogInformation("Application OnSleep");
    }

    protected override void OnResume()
    {
        _logger.LogInformation("Application OnResume");
    }
}