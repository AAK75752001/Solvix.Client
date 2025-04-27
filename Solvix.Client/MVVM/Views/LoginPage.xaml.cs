using Microsoft.Extensions.Logging;
using Solvix.Client.MVVM.ViewModels;

namespace Solvix.Client.MVVM.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;
    private readonly ILogger<LoginPage> _logger;

    public LoginPage(LoginViewModel viewModel)
    {
        try
        {
            // Create a console logger as fallback if no logger is provided
            _logger = new LoggerFactory().CreateLogger<LoginPage>();

            InitializeComponent();

            _viewModel = viewModel;
            BindingContext = _viewModel;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing LoginPage: {ex.Message}");

            // Create a simple UI to show the error
            Content = new VerticalStackLayout
            {
                Children =
                {
                    new Label { Text = "Error initializing login page", FontSize = 20, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40, 0, 0) },
                    new Label { Text = ex.Message, FontSize = 14, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(20) }
                },
                VerticalOptions = LayoutOptions.Center
            };
        }
    }

    protected override void OnAppearing()
    {
        try
        {
            base.OnAppearing();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in LoginPage.OnAppearing: {ex.Message}");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Disable back button on login page
        return true;
    }
}