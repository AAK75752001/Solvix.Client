using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Models;
using Solvix.Client.MVVM.ViewModels;

namespace Solvix.Client.MVVM.Views;

[QueryProperty(nameof(ChatId), "ChatId")]
[QueryProperty("Timestamp", "t")]
public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;
    private readonly ILogger<ChatPage> _logger;
    private string _chatId;
    public string Timestamp { get; set; }


    public string ChatId
    {
        get => _chatId;
        set
        {
            _chatId = value;
            if (_viewModel != null && !string.IsNullOrEmpty(_chatId))
            {
                _viewModel.ChatId = _chatId;
                _logger.LogInformation($"ChatPage received ChatId: {_chatId}");
            }
        }
    }



    public ChatPage(ChatViewModel viewModel, ILogger<ChatPage> logger)
    {
        try
        {
            InitializeComponent();

            _viewModel = viewModel;
            _logger = logger;

            // Important: Set BindingContext in constructor
            // But ChatId will be set later via QueryProperty
            BindingContext = _viewModel;

            _logger.LogInformation("ChatPage initialized");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing ChatPage");
        }
    }

    private void OnMessageBubbleLoaded(object sender, EventArgs e)
    {
        if (sender is Border messageBubble &&
            messageBubble.BindingContext is MessageModel message)
        {
            messageBubble.BackgroundColor = message.IsOwnMessage ?
                Colors.LightBlue : Colors.LightGray;
        }
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _logger.LogInformation("ChatPage appearing, ChatId: {ChatId}", _chatId);

        // This must run to update ChatViewModel with new ChatId
        if (_viewModel != null && !string.IsNullOrEmpty(_chatId))
        {
            _viewModel.ChatId = _chatId;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Let the base class handle the back button
        return base.OnBackButtonPressed();
    }
}