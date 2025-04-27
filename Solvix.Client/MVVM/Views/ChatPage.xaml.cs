using Microsoft.Extensions.Logging;
using Solvix.Client.MVVM.ViewModels;

namespace Solvix.Client.MVVM.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;
    private readonly ILogger<ChatPage> _logger;

    public ChatPage(ChatViewModel viewModel, ILogger<ChatPage> logger)
    {
        try
        {
            _logger = logger;
            _logger.LogInformation("Initializing ChatPage");

            InitializeComponent();

            _viewModel = viewModel;
            BindingContext = _viewModel;

            _logger.LogInformation("ChatPage initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing ChatPage");

            // Create a simple UI to show the error
            Content = new VerticalStackLayout
            {
                Children =
                {
                    new Label { Text = "Error loading chat", FontSize = 20, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40, 0, 0) },
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
            _logger.LogInformation("ChatPage OnAppearing");
            base.OnAppearing();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChatPage.OnAppearing");
        }
    }

    protected override void OnDisappearing()
    {
        try
        {
            _logger.LogInformation("ChatPage OnDisappearing");
            base.OnDisappearing();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChatPage.OnDisappearing");
        }
    }
}