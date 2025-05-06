using Solvix.Client.MVVM.ViewModels;
using System.Runtime.CompilerServices;

namespace Solvix.Client.MVVM.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatPageViewModel _viewModel;
    private CancellationTokenSource? _typingCancellationTokenSource;
    private readonly IDispatcherTimer _typingTimer;
    private bool _disposed = false;

    public ChatPage(ChatPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // ایجاد تایمر برای تشخیص وقفه در تایپ کردن
        _typingTimer = Dispatcher.CreateTimer();
        _typingTimer.Interval = TimeSpan.FromSeconds(2);
        _typingTimer.Tick += OnTypingTimerTick;
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

        // در صورت خروج از صفحه، وضعیت تایپ را به پایان می‌رسانیم
        StopTypingTimer();

        // رویداد TextChanged را حذف می‌کنیم
        if (this.FindByName("MessageEditor") is Editor messageEditor)
        {
            messageEditor.TextChanged -= OnMessageTextChanged;
        }

        // منابع را آزاد می‌کنیم
        Dispose();
    }

    private async void OnMessageTextChanged(object sender, TextChangedEventArgs e)
    {
        // اگر متن خالی شده، وضعیت تایپ را به پایان می‌رسانیم
        if (string.IsNullOrEmpty(e.NewTextValue))
        {
            StopTypingTimer();
            await _viewModel.UpdateTypingStatusAsync(false);
            return;
        }

        // اگر تغییری در متن ایجاد شده، تایمر را ریست می‌کنیم
        if (e.OldTextValue != e.NewTextValue)
        {
            RestartTypingTimer();
        }
    }

    private void RestartTypingTimer()
    {
        StopTypingTimer();

        // ایجاد CancellationTokenSource جدید
        _typingCancellationTokenSource = new CancellationTokenSource();
        var token = _typingCancellationTokenSource.Token;

        // اعلام وضعیت تایپ کردن به سرور
        Task.Run(async () =>
        {
            try
            {
                if (!token.IsCancellationRequested)
                {
                    await _viewModel.UpdateTypingStatusAsync(true);

                    // شروع تایمر برای تشخیص پایان تایپ کردن
                    MainThread.BeginInvokeOnMainThread(() => _typingTimer.Start());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in typing status: {ex.Message}");
            }
        }, token);
    }

    private void StopTypingTimer()
    {
        _typingTimer.Stop();

        _typingCancellationTokenSource?.Cancel();
        _typingCancellationTokenSource?.Dispose();
        _typingCancellationTokenSource = null;
    }

    private async void OnTypingTimerTick(object? sender, EventArgs e)
    {
        _typingTimer.Stop();
        await _viewModel.UpdateTypingStatusAsync(false);
    }

    private void Dispose()
    {
        if (_disposed) return;

        StopTypingTimer();
        _typingTimer.Tick -= OnTypingTimerTick;

        _disposed = true;
    }
}