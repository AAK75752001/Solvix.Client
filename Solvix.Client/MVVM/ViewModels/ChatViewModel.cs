using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using System.Collections.Specialized;
using Solvix.Client.Core.Helpers;
using CollectionExtensions = Solvix.Client.Core.Helpers.CollectionExtensions;

namespace Solvix.Client.MVVM.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly IChatService _chatService;
        private readonly ISignalRService _signalRService;
        private readonly IToastService _toastService;
        private readonly ILogger<ChatViewModel> _logger;

        private string _chatId;
        private ChatModel _chat;
        private string _messageText = string.Empty;
        private bool _isLoading;
        private bool _isSending;
        private bool _noMessages = false;
        private long _currentUserId = 0;
        private ObservableCollection<MessageModel> _messages = new();
        private bool _isInitialized = false;
        private bool _isLoadingMore;
        private int _messagesSkip = 0;
        private const int MessagesPageSize = 30;

        // Dictionary to track pending messages by their temporary ID
        private readonly Dictionary<int, MessageModel> _pendingMessages = new();

        public string ChatId
        {
            get => _chatId;
            set
            {
                if (_chatId != value)
                {
                    _chatId = value;
                    OnPropertyChanged();

                    if (!string.IsNullOrEmpty(_chatId) && !_isInitialized)
                    {
                        _isInitialized = true;
                        LoadChatAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set
            {
                if (_isLoadingMore != value)
                {
                    _isLoadingMore = value;
                    OnPropertyChanged();
                }
            }
        }

        private async Task<long> GetUserIdAsync()
        {
            try
            {
                if (_currentUserId > 0)
                    return _currentUserId;

                _currentUserId = await _chatService.GetCurrentUserIdAsync();
                return _currentUserId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user ID");
                return 0;
            }
        }

        public ChatModel Chat
        {
            get => _chat;
            set
            {
                if (_chat != value)
                {
                    _chat = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<MessageModel> Messages
        {
            get => _messages;
            private set
            {
                if (_messages != value)
                {
                    // اگر کالکشن جدید است، باید آبزرور‌های مناسب اضافه شوند
                    if (_messages != null)
                    {
                        _messages.CollectionChanged -= Messages_CollectionChanged;
                    }

                    _messages = value;

                    if (_messages != null)
                    {
                        _messages.CollectionChanged += Messages_CollectionChanged;
                    }

                    OnPropertyChanged();
                    NoMessages = _messages.Count == 0;
                }
            }
        }

        private void Messages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // به‌روزرسانی پرچم NoMessages هر زمان که کالکشن پیام‌ها تغییر می‌کند
            NoMessages = Messages.Count == 0;
        }

        public string MessageText
        {
            get => _messageText;
            set
            {
                if (_messageText != value)
                {
                    _messageText = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSendMessage));
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSending
        {
            get => _isSending;
            set
            {
                if (_isSending != value)
                {
                    _isSending = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSendMessage));
                }
            }
        }

        public bool NoMessages
        {
            get => _noMessages;
            set
            {
                if (_noMessages != value)
                {
                    _noMessages = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanSendMessage => !string.IsNullOrWhiteSpace(MessageText) && !IsSending;

        public ICommand SendMessageCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ViewProfileCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand LoadMoreMessagesCommand { get; }

        public ChatViewModel(
            IChatService chatService,
            ISignalRService signalRService,
            IToastService toastService,
            ILogger<ChatViewModel> logger)
        {
            _chatService = chatService;
            _signalRService = signalRService;
            _toastService = toastService;
            _logger = logger;

            SendMessageCommand = new Command(async () => await SendMessageAsync(), () => CanSendMessage);
            BackCommand = new Command(async () => await GoBackAsync());
            ViewProfileCommand = new Command(async () => await ViewProfileAsync());
            RefreshCommand = new Command(async () => await LoadChatAsync());
            LoadMoreMessagesCommand = new Command(async () => await LoadMoreMessagesAsync());

            // Subscribe to SignalR events
            _signalRService.OnMessageReceived += OnMessageReceived;
            _signalRService.OnMessageRead += OnMessageRead;
            _signalRService.OnUserStatusChanged += OnUserStatusChanged;
            _signalRService.OnMessageConfirmed += OnMessageConfirmed;

            _logger.LogInformation("ChatViewModel initialized");
        }

        private async Task LoadChatAsync()
        {
            if (string.IsNullOrEmpty(ChatId))
            {
                _logger.LogWarning("ChatId is empty - cannot load chat");
                return;
            }

            if (!Guid.TryParse(ChatId, out var chatGuid))
            {
                _logger.LogError("Invalid ChatId format: {ChatId}", ChatId);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await _toastService.ShowToastAsync("Invalid chat ID", ToastType.Error));
                return;
            }

            try
            {
                if (Messages.Count == 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => IsLoading = true);
                }

                // Load chat info
                var chat = await _chatService.GetChatAsync(chatGuid);
                if (chat == null)
                {
                    _logger.LogWarning("Chat not found: {ChatId}", chatGuid);
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        IsLoading = false;
                        await _toastService.ShowToastAsync("Chat not found", ToastType.Error);
                    });
                    return;
                }

                // Load messages
                var messages = await _chatService.GetMessagesAsync(chatGuid);
                _logger.LogInformation("Loaded {Count} messages for chat {ChatId}",
                    messages?.Count ?? 0, chatGuid);

                // Ensure user ID is loaded
                if (_currentUserId == 0)
                {
                    _currentUserId = await GetUserIdAsync();
                }

                // Update UI on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // Set chat
                    Chat = chat;

                    // If messages were loaded
                    if (messages != null && messages.Count > 0)
                    {
                        // Mark messages as own or not
                        foreach (var message in messages)
                        {
                            message.IsOwnMessage = message.SenderId == _currentUserId;

                            // Format time if not already set
                            if (string.IsNullOrEmpty(message.SentAtFormatted))
                            {
                                message.SentAtFormatted = FormatTimeDisplay(message.SentAt);
                            }
                        }

                        // Sort by sent time and update collection
                        var sortedMessages = messages.OrderBy(m => m.SentAt).ToList();
                        Messages = new ObservableCollection<MessageModel>(sortedMessages);
                    }
                    else if (messages != null && messages.Count == 0)
                    {
                        Messages = new ObservableCollection<MessageModel>();
                        NoMessages = true;
                    }

                    // Finish loading
                    IsLoading = false;
                });

                // Mark unread messages as read
                await MarkUnreadMessagesAsReadAsync();

                _messagesSkip = messages?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat {ChatId}", chatGuid);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsLoading = false;
                    _toastService.ShowToastAsync($"Error loading chat: {ex.Message}", ToastType.Error);
                });
            }
        }

        private async Task MarkUnreadMessagesAsReadAsync()
        {
            if (Chat == null || string.IsNullOrEmpty(ChatId) ||
                !Guid.TryParse(ChatId, out var chatGuid))
                return;

            try
            {
                // Find unread messages not sent by current user
                var unreadMessageIds = Messages
                    .Where(m => !m.IsOwnMessage && !m.IsRead && m.Id > 0)
                    .Select(m => m.Id)
                    .ToList();

                if (unreadMessageIds.Count > 0)
                {
                    _logger.LogInformation("Marking {Count} messages as read", unreadMessageIds.Count);

                    // Mark as read on server
                    await _chatService.MarkAsReadAsync(chatGuid, unreadMessageIds);

                    // Update local message status
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var messageId in unreadMessageIds)
                        {
                            var message = Messages.FirstOrDefault(m => m.Id == messageId);
                            if (message != null)
                            {
                                message.IsRead = true;
                                message.ReadAt = DateTime.UtcNow;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read");
            }
        }

        private async Task SendMessageAsync()
        {
            if (!CanSendMessage || string.IsNullOrEmpty(ChatId) || !Guid.TryParse(ChatId, out var chatGuid))
                return;

            string messageText = MessageText.Trim();
            MessageText = string.Empty;

            try
            {
                IsSending = true;
                _logger.LogInformation("Sending message to chat {ChatId}: {MessageText}", chatGuid, messageText);

                // ایجاد یک ID منفی و تصادفی برای پیام موقت
                var tempId = -1 * new Random().Next(10000, 99999);

                // ایجاد پیام موقت
                var tempMessage = new MessageModel
                {
                    Id = tempId,
                    Content = messageText,
                    SentAt = DateTime.Now,
                    ChatId = chatGuid,
                    SenderId = await GetUserIdAsync(),
                    SenderName = "You",
                    Status = Constants.MessageStatus.Sending,
                    SentAtFormatted = DateTime.Now.ToString("HH:mm"),
                    IsOwnMessage = true
                };

                // اضافه کردن پیام به کالکشن بدون ساخت کالکشن جدید
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Messages.Add(tempMessage);
                    NoMessages = false;
                });

                // ارسال پیام به سرور
                MessageModel serverMessage = await _chatService.SendMessageAsync(chatGuid, messageText);

                // به‌روزرسانی پیام موقت با پاسخ سرور
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // یافتن پیام موقت در کالکشن
                    var index = -1;
                    for (int i = 0; i < Messages.Count; i++)
                    {
                        if (Messages[i].Id == tempId)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index >= 0)
                    {
                        if (serverMessage != null)
                        {
                            // به‌روزرسانی پیام با داده‌های سرور
                            Messages[index].Id = serverMessage.Id;
                            Messages[index].Status = Constants.MessageStatus.Sent;

                            // اعلان تغییرات برای به‌روزرسانی UI
                            CollectionExtensions.UpdateItem(Messages, index);
                        }
                        else
                        {
                            // علامت‌گذاری به عنوان ناموفق در صورت عدم پاسخ سرور
                            Messages[index].Status = Constants.MessageStatus.Failed;

                            // اعلان تغییرات برای به‌روزرسانی UI
                            CollectionExtensions.UpdateItem(Messages, index);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendMessageAsync: {Message}", ex.Message);
                await _toastService.ShowToastAsync($"Error sending message: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsSending = false;
            }
        }

        private string FormatTimeDisplay(DateTime dateTime)
        {
            try
            {
                // Ensure we're using local time
                DateTime localTime = dateTime.Kind == DateTimeKind.Utc
                    ? dateTime.ToLocalTime()
                    : dateTime;

                return localTime.ToString("HH:mm");
            }
            catch
            {
                // Fall back to simple format in case of error
                return dateTime.ToString("HH:mm");
            }
        }

        private async Task GoBackAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating back");
            }
        }

        private async Task ViewProfileAsync()
        {
            if (Chat?.OtherParticipant == null)
                return;

            await _toastService.ShowToastAsync("Profile viewing will be available in a future update", ToastType.Info);
        }

        private void OnMessageReceived(MessageModel message)
        {
            if (Chat == null || message.ChatId.ToString() != ChatId)
                return;

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    _logger.LogInformation("Received message {MessageId} for chat {ChatId}", message.Id, message.ChatId);

                    // تعیین اینکه آیا پیام مال خود کاربر است
                    message.IsOwnMessage = message.SenderId == _currentUserId;

                    // بررسی تکراری نبودن - ابتدا با ID
                    if (message.Id > 0)
                    {
                        // جستجو با ID دقیق
                        var existingById = Messages.FirstOrDefault(m => m.Id == message.Id);
                        if (existingById != null)
                        {
                            _logger.LogInformation("پیام تکراری (همان ID) شناسایی شد: {MessageId}", message.Id);
                            return; // عدم افزودن موارد تکراری
                        }
                    }

                    // برای پیام‌های خود کاربر، جستجوی پیام‌های موقتی که باید جایگزین شوند
                    if (message.IsOwnMessage)
                    {
                        // جستجوی پیام موقت با محتوای یکسان و زمان مشابه
                        var tempMessageIndex = -1;
                        for (int i = 0; i < Messages.Count; i++)
                        {
                            var m = Messages[i];
                            if (m.Id < 0 && // ID منفی = موقت
                                m.IsOwnMessage &&
                                m.Content == message.Content &&
                                Math.Abs((m.SentAt - message.SentAt).TotalSeconds) < 30)
                            {
                                tempMessageIndex = i;
                                break;
                            }
                        }

                        if (tempMessageIndex >= 0)
                        {
                            _logger.LogInformation("جایگزینی پیام موقت با پیام سرور");

                            // جایگزینی پیام موقت
                            Messages[tempMessageIndex] = message;
                            CollectionExtensions.UpdateItem(Messages, tempMessageIndex);
                            return; // عدم ادامه، جایگزینی انجام شد
                        }
                    }

                    // اگر تکراری نیست، به کالکشن اضافه کن
                    Messages.Add(message);
                    NoMessages = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در پردازش پیام دریافتی");
                }
            });
        }

        private void OnMessageConfirmed(int serverMessageId) // Using serverMessageId for clarity
        {
            _logger.LogInformation("Message with server ID {ServerMessageId} confirmed by server", serverMessageId);

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    // Find the message in the collection using its FINAL Server ID
                    var messageToUpdate = Messages.FirstOrDefault(m => m.Id == serverMessageId);

                    if (messageToUpdate != null)
                    {
                        // Update status. This event name "Confirmed" might map to "Sent" (one tick)
                        // or "Delivered" (two gray ticks) depending on your backend logic.
                        // Ensure this doesn't conflict if OnMessageReceived also updates the status.
                        // A common pattern: OnMessageReceived (with echo + corr ID) sets Status to Sent.
                        // OnMessageConfirmed sets Status to Delivered. OnMessageRead sets Status to Read.
                        messageToUpdate.Status = Constants.MessageStatus.Delivered; // Example: Use for Delivered status

                        // Notify UI that the Status property on the EXISTING object has changed
                        messageToUpdate.OnPropertyChanged(nameof(MessageModel.Status));
                        messageToUpdate.OnPropertyChanged(nameof(MessageModel.IsRead)); // If status implies read state change


                        _logger.LogInformation("Updated message {ServerMessageId} status to Delivered/Confirmed", serverMessageId);

                        // If this event confirms a message that was still pending (in _pendingMessages), remove it.
                        // This requires finding the temp ID from the server ID.
                        // The most reliable way is if the pending message object's ID is now the server ID
                        // (meaning OnMessageReceived already updated it).
                        var pendingToRemove = _pendingMessages.FirstOrDefault(kvp => kvp.Value.Id == serverMessageId); // Find in pending by its *current* ID (should be server ID)
                        if (pendingToRemove.Value != null)
                        {
                            _pendingMessages.Remove(pendingToRemove.Key);
                            _logger.LogInformation("Removed pending message with temp ID {TempId} from pending after OnMessageConfirmed via Server ID {ServerId}", pendingToRemove.Key, serverMessageId);
                        }
                        else
                        {
                            // This can happen if OnMessageReceived already removed it, or if this event
                            // fires for a message that wasn't sent in this client session or wasn't pending.
                            _logger.LogInformation("Message {ServerMessageId} not found in pending dictionary on OnMessageConfirmed.", serverMessageId);
                        }

                    }
                    else
                    {
                        _logger.LogWarning("Message with server ID {ServerMessageId} not found in collection to update status on confirmation.", serverMessageId);
                        // This could happen if the list is paginated and the message is not currently loaded.
                        // Or if OnMessageReceived somehow removed it? (shouldn't happen with the new logic)
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating message status on confirmation");
                }
            });
        }

        private async Task MarkMessageAsReadAsync(int messageId)
        {
            try
            {
                if (string.IsNullOrEmpty(ChatId) || !Guid.TryParse(ChatId, out var chatGuid))
                    return;

                await _chatService.MarkAsReadAsync(chatGuid, new List<int> { messageId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as read", messageId);
            }
        }

        private void OnMessageRead(Guid chatId, int messageId)
        {
            if (Chat == null || chatId.ToString() != ChatId)
                return;

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    _logger.LogInformation("Message {MessageId} in chat {ChatId} marked as read", messageId, chatId);

                    // یافتن پیام در کالکشن
                    var index = -1;
                    for (int i = 0; i < Messages.Count; i++)
                    {
                        if (Messages[i].Id == messageId && Messages[i].IsOwnMessage)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index >= 0)
                    {
                        // به‌روزرسانی وضعیت مستقیم
                        Messages[index].IsRead = true;
                        Messages[index].ReadAt = DateTime.UtcNow;
                        Messages[index].Status = Constants.MessageStatus.Read;

                        // به‌روزرسانی UI
                        CollectionExtensions.UpdateItem(Messages, index);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message read status");
                }
            });
        }


        private async Task LoadMoreMessagesAsync()
        {
            if (IsLoadingMore || string.IsNullOrEmpty(ChatId) || !Guid.TryParse(ChatId, out var chatGuid))
                return;

            try
            {
                // Only continue if we have messages and haven't reached the end of the list
                if (Messages.Count == 0 || _messagesSkip <= 0)
                    return;

                IsLoadingMore = true;
                _logger.LogInformation("Loading more messages from skip={Skip}", _messagesSkip);

                // Load more messages from server
                var messages = await _chatService.GetMessagesAsync(chatGuid, _messagesSkip, MessagesPageSize);

                if (messages != null && messages.Count > 0)
                {
                    _logger.LogInformation("Loaded {Count} more messages", messages.Count);

                    // Update skip point for next time
                    _messagesSkip += messages.Count;

                    // Set IsOwnMessage for each message
                    foreach (var message in messages)
                    {
                        message.IsOwnMessage = message.SenderId == _currentUserId;

                        // Format time if not already set
                        if (string.IsNullOrEmpty(message.SentAtFormatted))
                        {
                            message.SentAtFormatted = FormatTimeDisplay(message.SentAt);
                        }
                    }

                    // Add to beginning of list - these are older messages
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        var existingMessages = Messages.ToList();
                        var combined = messages.Union(existingMessages)
                            .OrderBy(m => m.SentAt)
                            .ToList();

                        Messages = new ObservableCollection<MessageModel>(combined);
                    });
                }
                else
                {
                    _logger.LogInformation("No more messages available");
                    // We've reached the end - no more messages to load
                    _messagesSkip = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading more messages");
                await _toastService.ShowToastAsync("Error loading older messages", ToastType.Error);
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private void OnUserStatusChanged(long userId, bool isOnline, DateTime? lastActive)
        {
            if (Chat?.OtherParticipant?.Id == userId)
            {
                MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        _logger.LogInformation("User {UserId} status changed: Online = {IsOnline}, LastActive = {LastActive}",
                            userId, isOnline, lastActive);

                        // Update online status
                        Chat.OtherParticipant.IsOnline = isOnline;
                        Chat.OtherParticipant.LastActive = lastActive;

                        // Force UI update
                        OnPropertyChanged(nameof(Chat));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating user status");
                    }
                });
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}