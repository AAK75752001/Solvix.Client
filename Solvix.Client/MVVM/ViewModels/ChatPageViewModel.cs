using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;

namespace Solvix.Client.MVVM.ViewModels
{
    [QueryProperty(nameof(ChatIdString), "ChatId")]
    public partial class ChatPageViewModel : ObservableObject, IDisposable
    {
        #region Services and Logger
        private readonly IChatService _chatService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        private readonly ISignalRService _signalRService;
        private readonly ILogger<ChatPageViewModel> _logger;
        #endregion

        #region Private Fields
        private long _currentUserId;
        private string? _chatIdString;
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        private readonly SemaphoreSlim _loadMessagesSemaphore = new SemaphoreSlim(1, 1);
        #endregion

        #region Observable Properties

        [ObservableProperty]
        private Guid _actualChatId;

        [ObservableProperty]
        private ChatModel? _currentChat;

        [ObservableProperty]
        private ObservableCollection<MessageModel> _messages = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText = string.Empty;

        [ObservableProperty]
        private bool _isLoadingMessages;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private bool _isSendingMessage;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isTyping;

        #endregion

        #region Public Properties

        public string? ChatIdString
        {
            get => _chatIdString;
            set
            {
                if (_chatIdString == value) return;
                _chatIdString = value;

                if (Guid.TryParse(value, out Guid parsedGuid))
                {
                    if (parsedGuid != ActualChatId)
                    {
                        ActualChatId = parsedGuid;
                        _logger.LogInformation("ChatId received and parsed: {ActualChatId}. Initializing chat.", ActualChatId);
                        MainThread.BeginInvokeOnMainThread(async () => await InitializeChatAsync());
                    }
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    _logger.LogError("Failed to parse received ChatId string: '{ChatIdString}'", value);
                    HandleInvalidChatId();
                }
            }
        }

        #endregion

        #region Constructor
        public ChatPageViewModel(
            IChatService chatService,
            IToastService toastService,
            IAuthService authService,
            ISignalRService signalRService,
            ILogger<ChatPageViewModel> logger)
        {
            _chatService = chatService;
            _toastService = toastService;
            _authService = authService;
            _signalRService = signalRService;
            _logger = logger;

            // ثبت رویدادهای SignalR
            _signalRService.OnMessageReceived += SignalRMessageReceived;
            _signalRService.OnMessageStatusUpdated += SignalRMessageStatusUpdated;
            _signalRService.OnConnectionStateChanged += SignalRConnectionStateChanged;

            // وضعیت اتصال اولیه
            IsConnected = _signalRService.IsConnected;
        }
        #endregion

        #region Commands

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            var messageContentToSend = NewMessageText;
            if (string.IsNullOrWhiteSpace(messageContentToSend)) return;

            NewMessageText = string.Empty;
            IsSendingMessage = true;

            var optimisticMessage = CreateOptimisticMessage(messageContentToSend);
            Messages.Add(optimisticMessage);

            try
            {
                // ابتدا تلاش می‌کنیم از طریق SignalR ارسال کنیم
                bool signalRSuccess = false;
                if (_signalRService.IsConnected)
                {
                    signalRSuccess = await _signalRService.SendMessageAsync(optimisticMessage);
                }

                // اگر SignalR موفق نبود، از طریق API ارسال می‌کنیم
                if (!signalRSuccess)
                {
                    var sentMessageDto = await _chatService.SendMessageAsync(ActualChatId, messageContentToSend);
                    UpdateSentMessageStatus(optimisticMessage, sentMessageDto);
                }
                else
                {
                    // پیام با موفقیت از طریق SignalR ارسال شد، وضعیت را به "ارسال‌شده" تغییر می‌دهیم
                    optimisticMessage.Status = Constants.MessageStatus.Sent;
                }
            }
            catch (Exception ex)
            {
                HandleMessageSendError(ex, optimisticMessage);
            }
            finally
            {
                IsSendingMessage = false;
            }
        }

        [RelayCommand]
        private async Task LoadMoreMessagesAsync()
        {
            await _loadMessagesSemaphore.WaitAsync();
            try
            {
                if (IsLoadingMessages || ActualChatId == Guid.Empty) return;

                _logger.LogInformation("Attempting to load more messages for chat {ActualChatId}", ActualChatId);
                IsLoadingMessages = true;

                int currentMessageCount = Messages.Count;
                var olderMessages = await _chatService.GetChatMessagesAsync(ActualChatId, currentMessageCount, 30);

                if (olderMessages != null && olderMessages.Any())
                {
                    _currentUserId = await _authService.GetUserIdAsync();

                    foreach (var msg in olderMessages.OrderByDescending(m => m.SentAt))
                    {
                        msg.IsOwnMessage = msg.SenderId == _currentUserId;

                        // بررسی می‌کنیم پیام قبلا اضافه نشده باشد
                        if (!Messages.Any(m => (m.Id > 0 && m.Id == msg.Id) ||
                                            (m.Id <= 0 && m.Content == msg.Content &&
                                             Math.Abs((m.SentAt - msg.SentAt).TotalSeconds) < 10)))
                        {
                            Messages.Insert(0, msg);
                        }
                    }
                    _logger.LogInformation("Loaded {Count} older messages for chat {ActualChatId}", olderMessages.Count, ActualChatId);
                }
                else
                {
                    _logger.LogInformation("No more older messages found for chat {ActualChatId}", ActualChatId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading more messages for chat {ActualChatId}", ActualChatId);
                await _toastService.ShowToastAsync("خطا در بارگذاری پیام‌های قبلی.", ToastType.Error);
            }
            finally
            {
                IsLoadingMessages = false;
                _loadMessagesSemaphore.Release();
            }
        }

        [RelayCommand]
        private async Task AttachFileAsync() => await _toastService.ShowToastAsync("ارسال فایل (به زودی!)", ToastType.Info);

        [RelayCommand]
        private async Task EmojiAsync() => await _toastService.ShowToastAsync("انتخاب ایموجی (به زودی!)", ToastType.Info);

        [RelayCommand]
        private async Task GoToChatSettingsAsync() => await _toastService.ShowToastAsync("تنظیمات چت (به زودی!)", ToastType.Info);

        [RelayCommand]
        private async Task MarkAllAsReadAsync()
        {
            try
            {
                var unreadMessages = Messages
                    .Where(m => !m.IsOwnMessage && !m.IsRead && m.Id > 0)
                    .Select(m => m.Id)
                    .ToList();

                if (!unreadMessages.Any()) return;

                await _chatService.MarkMessagesAsReadAsync(ActualChatId, unreadMessages);

                // اگر SignalR وصل است، وضعیت را از آنجا آپدیت می‌کنیم
                foreach (var msgId in unreadMessages)
                {
                    if (_signalRService.IsConnected)
                    {
                        await _signalRService.MarkAsReadAsync(ActualChatId, msgId);
                    }

                    // آپدیت کردن وضعیت پیام در UI
                    var message = Messages.FirstOrDefault(m => m.Id == msgId);
                    if (message != null)
                    {
                        message.IsRead = true;
                        message.ReadAt = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read");
            }
        }

        #endregion

        #region SignalR Event Handlers

        private void SignalRMessageReceived(MessageModel message)
        {
            if (message.ChatId != ActualChatId) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // بررسی تکراری بودن پیام
                if (!Messages.Any(m => (m.Id > 0 && m.Id == message.Id) ||
                                    (m.CorrelationId == message.CorrelationId && !string.IsNullOrEmpty(m.CorrelationId))))
                {
                    // تعیین مالکیت پیام
                    message.IsOwnMessage = message.SenderId == _currentUserId;

                    Messages.Add(message);

                    // اگر پیام برای ما ارسال شده، آن را به عنوان خوانده‌شده علامت می‌زنیم
                    if (!message.IsOwnMessage && message.Id > 0)
                    {
                        // فقط در حالتی که صفحه در فوکوس است، پیام را خوانده‌شده علامت می‌زنیم
                        // در اینجا فرض می‌کنیم صفحه همیشه در فوکوس است
                        Task.Run(async () =>
                        {
                            try
                            {
                                await _signalRService.MarkAsReadAsync(ActualChatId, message.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error marking message as read via SignalR");
                            }
                        });
                    }
                }
            });
        }

        private void SignalRMessageStatusUpdated(Guid chatId, int messageId, int status)
        {
            if (chatId != ActualChatId) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var message = Messages.FirstOrDefault(m => m.Id == messageId);
                if (message != null)
                {
                    // بروزرسانی وضعیت پیام
                    if (status > message.Status || status == Constants.MessageStatus.Failed)
                    {
                        message.Status = status;

                        // اگر پیام خوانده شده است، زمان خواندن را ثبت می‌کنیم
                        if (status == Constants.MessageStatus.Read && !message.ReadAt.HasValue)
                        {
                            message.ReadAt = DateTime.UtcNow;
                            message.IsRead = true;
                        }
                    }
                }
            });
        }

        private void SignalRConnectionStateChanged(bool isConnected)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsConnected = isConnected;
            });
        }

        #endregion

        #region Private Methods

        private async Task InitializeChatAsync()
        {
            if (ActualChatId == Guid.Empty || _isInitialized) return;

            await _loadMessagesSemaphore.WaitAsync();
            try
            {
                IsLoadingMessages = true;

                if (!_isInitialized)
                {
                    Messages.Clear();
                }

                _currentUserId = await _authService.GetUserIdAsync();

                if (_currentUserId == 0)
                {
                    _logger.LogError("InitializeChatAsync failed: Could not get current user ID.");
                    await _toastService.ShowToastAsync("خطا در شناسایی کاربر.", ToastType.Error);
                    return;
                }

                // اطمینان از اتصال SignalR
                if (!_signalRService.IsConnected)
                {
                    await _signalRService.StartAsync();
                }

                CurrentChat = await _chatService.GetChatByIdAsync(ActualChatId);

                if (!_isInitialized || !Messages.Any())
                {
                    var initialMessages = await _chatService.GetChatMessagesAsync(ActualChatId, 0, 50);
                    if (initialMessages != null)
                    {
                        foreach (var msg in initialMessages.OrderBy(m => m.SentAt))
                        {
                            msg.IsOwnMessage = msg.SenderId == _currentUserId;
                            if (!Messages.Any(m => m.Id == msg.Id && m.Id > 0))
                            {
                                Messages.Add(msg);
                            }
                        }
                        _logger.LogInformation("Loaded {Count} initial messages for Chat {ActualChatId}", initialMessages.Count, ActualChatId);
                    }
                    else
                    {
                        _logger.LogWarning("InitializeChatAsync: GetChatMessagesAsync returned null for ChatId {ActualChatId}", ActualChatId);
                    }
                }

                _isInitialized = true;

                // خواندن پیام‌های جدید
                await MarkAllAsReadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing chat page for ChatId {ActualChatId}", ActualChatId);
                await _toastService.ShowToastAsync("خطا در بارگذاری پیام‌ها.", ToastType.Error);
            }
            finally
            {
                IsLoadingMessages = false;
                _loadMessagesSemaphore.Release();
            }
        }

        private bool CanSendMessage() => !string.IsNullOrWhiteSpace(NewMessageText) && !IsSendingMessage;

        private MessageModel CreateOptimisticMessage(string content)
        {
            return new MessageModel
            {
                Id = 0,
                ChatId = ActualChatId,
                Content = content,
                SenderId = _currentUserId,
                SentAt = DateTime.Now,
                IsOwnMessage = true,
                Status = Constants.MessageStatus.Sending,
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        private void UpdateSentMessageStatus(MessageModel optimisticMessage, MessageModel? serverMessage)
        {
            var messageToUpdate = Messages.FirstOrDefault(m => m.CorrelationId == optimisticMessage.CorrelationId);
            if (messageToUpdate != null)
            {
                if (serverMessage != null)
                {
                    messageToUpdate.Id = serverMessage.Id;
                    messageToUpdate.Status = Constants.MessageStatus.Sent;
                    messageToUpdate.SentAt = serverMessage.SentAt;
                    messageToUpdate.CorrelationId = string.Empty;
                    _logger.LogInformation("Optimistic message updated with server info. ID: {Id}", serverMessage.Id);
                }
                else
                {
                    messageToUpdate.Status = Constants.MessageStatus.Failed;
                    _logger.LogWarning("SendMessageAsync returned null, marking optimistic message as Failed. CorrelationId: {CorrId}", optimisticMessage.CorrelationId);
                }
            }
            else
            {
                _logger.LogWarning("Could not find optimistic message to update. CorrelationId: {CorrId}", optimisticMessage.CorrelationId);
            }
        }

        private async void HandleMessageSendError(Exception ex, MessageModel optimisticMessage)
        {
            _logger.LogError(ex, "Error sending message to chat {ActualChatId}", ActualChatId);
            await _toastService.ShowToastAsync("خطا در ارسال پیام.", ToastType.Error);

            var messageToUpdate = Messages.FirstOrDefault(m => m.CorrelationId == optimisticMessage.CorrelationId);
            if (messageToUpdate != null)
            {
                messageToUpdate.Status = Constants.MessageStatus.Failed;
            }
        }

        private void HandleInvalidChatId()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await _toastService.ShowToastAsync("خطا: شناسه چت نامعتبر است.", ToastType.Error);
                if (Shell.Current.Navigation.NavigationStack.Count > 1)
                    await Shell.Current.Navigation.PopAsync();
                else
                    await Shell.Current.GoToAsync("..");
            });
        }

        public async Task UpdateTypingStatusAsync(bool isTyping)
        {
            if (IsTyping == isTyping) return;

            IsTyping = isTyping;

            if (_signalRService.IsConnected)
            {
                await _signalRService.TypingAsync(ActualChatId, isTyping);
            }
        }
        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _signalRService.OnMessageReceived -= SignalRMessageReceived;
                _signalRService.OnMessageStatusUpdated -= SignalRMessageStatusUpdated;
                _signalRService.OnConnectionStateChanged -= SignalRConnectionStateChanged;

                _loadMessagesSemaphore.Dispose();
            }

            _isDisposed = true;
        }

        #endregion
    }
}