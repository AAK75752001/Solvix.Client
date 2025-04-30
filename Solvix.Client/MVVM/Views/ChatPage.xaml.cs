using Microsoft.Extensions.Logging;
using Solvix.Client.MVVM.ViewModels;

namespace Solvix.Client.MVVM.Views;

[QueryProperty(nameof(ChatId), "ChatId")]
public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;
    private readonly ILogger<ChatPage> _logger;
    private string _chatId;

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

            // مهم: وابستگی را در سازنده تنظیم می‌کنیم
            // اما ChatId را باید بعداً از QueryProperty بگیریم
            BindingContext = _viewModel;

            _logger.LogInformation("ChatPage initialized");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing ChatPage");
        }
    }


    protected override void OnAppearing()
    {
        base.OnAppearing();
        _logger.LogInformation("ChatPage appearing, ChatId: {ChatId}", _chatId);

        // این حتماً باید اجرا شود تا ChatViewModel با ChatId جدید آپدیت شود
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