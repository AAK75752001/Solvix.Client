using Solvix.Client.MVVM.ViewModels;
using Microsoft.Extensions.Logging;

namespace Solvix.Client.MVVM.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatPageViewModel _viewModel;
    private readonly ILogger<ChatPage> _logger;
    private CancellationTokenSource? _typingCancellationTokenSource;
    private bool _isTyping = false;
    private DateTime _lastTypingTime = DateTime.MinValue;
    private bool _disposed = false;

    public ChatPage(ChatPageViewModel viewModel, ILogger<ChatPage> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _logger = logger;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // در صورتی که دارای ورودی پیام هستیم، رویداد TextChanged را اضافه می‌کنیم
        if (this.FindByName("MessageEditor") is Editor messageEditor)
        {
            messageEditor.TextChanged += OnMessageTextChanged;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _disposed = true;

        // Ensure typing status is reset
        if (_isTyping)
        {
            _viewModel.UpdateTypingStatusAsync(false).ConfigureAwait(false);
            _isTyping = false;
        }

        // Clean up
        _typingCancellationTokenSource?.Cancel();
        _typingCancellationTokenSource?.Dispose();
        _typingCancellationTokenSource = null;

        // Remove event handler
        if (this.FindByName("MessageEditor") is Editor messageEditor)
        {
            messageEditor.TextChanged -= OnMessageTextChanged;
        }
    }

    private void OnRemainingItemsThresholdReached(object sender, EventArgs e)
    {
        if (_viewModel.CanLoadMore && !_viewModel.IsLoadingMoreMessages)
        {
            _viewModel.LoadMoreMessagesCommand.Execute(null);
        }
    }

    private async void OnMessageTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_disposed) return;

        // Cancel previous typing detection
        _typingCancellationTokenSource?.Cancel();
        _typingCancellationTokenSource?.Dispose();
        _typingCancellationTokenSource = new CancellationTokenSource();
        var token = _typingCancellationTokenSource.Token;

        try
        {
            // Check if text is empty
            if (string.IsNullOrEmpty(e.NewTextValue))
            {
                if (_isTyping)
                {
                    await _viewModel.UpdateTypingStatusAsync(false);
                    _isTyping = false;
                }
                return;
            }

            // Only send typing status if not already typing or if enough time has passed
            var now = DateTime.UtcNow;
            if (!_isTyping || (now - _lastTypingTime).TotalSeconds > 3)
            {
                _isTyping = true;
                _lastTypingTime = now;
                await _viewModel.UpdateTypingStatusAsync(true);
            }

            // Wait for 2 seconds of inactivity before sending "stopped typing"
            await Task.Delay(2000, token);

            if (!token.IsCancellationRequested && _isTyping)
            {
                _isTyping = false;
                await _viewModel.UpdateTypingStatusAsync(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when typing continues
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling typing status");
        }
    }
}