using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Services;

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
        private readonly Dictionary<int, int> _tempToServerMessageIds = new();

        public string ChatId
        {
            get => _chatId;
            set
            {
                if (_chatId != value)
                {
                    _chatId = value;
                    OnPropertyChanged();

                    // Load chat when ChatId is set
                    if (!string.IsNullOrEmpty(_chatId) && !_isInitialized)
                    {
                        _isInitialized = true;
                        LoadChatAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        private long GetUserIdSync()
        {
            try
            {
                // Try to get from field first
                if (_currentUserId > 0)
                    return _currentUserId;

                // Try to get synchronously from secure storage
                var userIdTask = _chatService.GetCurrentUserIdAsync();
                if (userIdTask.IsCompleted)
                {
                    _currentUserId = userIdTask.Result;
                    return _currentUserId;
                }

                // If task not completed, wait a short time
                userIdTask.Wait(100);  // Wait up to 100ms
                if (userIdTask.IsCompleted)
                {
                    _currentUserId = userIdTask.Result;
                    return _currentUserId;
                }

                // If still not available, return 0
                return 0;
            }
            catch
            {
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

                    // If chat messages exist, update our messages collection
                    if (_chat?.Messages != null && _chat.Messages.Count > 0)
                    {
                        UpdateMessagesCollection(_chat.Messages.ToList());
                    }
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
                    _messages = value;
                    OnPropertyChanged();
                    NoMessages = _messages.Count == 0;
                }
            }
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

            // Subscribe to SignalR events
            _signalRService.OnMessageReceived += OnMessageReceived;
            _signalRService.OnMessageRead += OnMessageRead;
            _signalRService.OnUserStatusChanged += OnUserStatusChanged;

            _logger.LogInformation("ChatViewModel initialized");
        }

        // Smoothly update the messages collection without replacing it completely
        private void UpdateMessagesCollection(List<MessageModel> newMessages)
        {
            try
            {
                if (newMessages == null || newMessages.Count == 0)
                {
                    // فقط در صورتی که پیامی در کالکشن موجود باشد، آن را پاک می‌کنیم
                    if (Messages.Count > 0)
                    {
                        MainThread.BeginInvokeOnMainThread(() => {
                            Messages.Clear();
                            NoMessages = true;
                        });
                    }
                    return;
                }

                // Set NoMessages based on new message count
                NoMessages = newMessages.Count == 0;

                // به جای جایگزینی کل کالکشن، فقط پیام‌های جدید را اضافه می‌کنیم
                MainThread.BeginInvokeOnMainThread(() => {
                    // تهیه لیست پیام‌هایی که در حال حاضر در کالکشن موجود نیستند
                    var currentMessageIds = new HashSet<int>(Messages.Where(m => m.Id > 0).Select(m => m.Id));
                    var messagesToAdd = newMessages.Where(m => m.Id > 0 && !currentMessageIds.Contains(m.Id)).ToList();

                    if (messagesToAdd.Count > 0)
                    {
                        // مرتب‌سازی پیام‌های جدید بر اساس زمان ارسال
                        messagesToAdd = messagesToAdd.OrderBy(m => m.SentAt).ToList();

                        // Preserve temporary messages (negative IDs)
                        var tempMessages = Messages.Where(m => m.Id < 0).ToList();

                        // اضافه کردن پیام‌های جدید به کالکشن موجود
                        foreach (var msg in messagesToAdd)
                        {
                            // We need to set IsOwnMessage again here
                            msg.IsOwnMessage = msg.SenderId == _currentUserId;

                            // Add to end of collection to avoid UI jumps
                            Messages.Add(msg);
                        }

                        // Re-sort messages if needed
                        var messagesList = Messages.OrderBy(m => m.SentAt).ToList();

                        // Only refresh collection if order has changed
                        bool orderChanged = false;
                        for (int i = 0; i < Messages.Count; i++)
                        {
                            if (i < messagesList.Count && Messages[i] != messagesList[i])
                            {
                                orderChanged = true;
                                break;
                            }
                        }

                        if (orderChanged)
                        {
                            var observableCopy = new ObservableCollection<MessageModel>(messagesList);
                            Messages.Clear();
                            foreach (var msg in observableCopy)
                            {
                                Messages.Add(msg);
                            }
                        }
                    }

                    // بروزرسانی پیام‌های موجود (مثلاً وضعیت خوانده شدن)
                    foreach (var message in newMessages)
                    {
                        var existingMessage = Messages.FirstOrDefault(m => m.Id == message.Id && m.Id > 0);
                        if (existingMessage != null)
                        {
                            // فقط اگر وضعیت تغییر کرده باشد، آن را به‌روز می‌کنیم
                            if (existingMessage.Status != message.Status ||
                                existingMessage.IsRead != message.IsRead ||
                                existingMessage.ReadAt != message.ReadAt)
                            {
                                existingMessage.Status = message.Status;
                                existingMessage.IsRead = message.IsRead;
                                existingMessage.ReadAt = message.ReadAt;

                                // Force UI update without collection replacement
                                OnPropertyChanged(nameof(Messages));
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating messages collection");
            }
        }

        private async Task FixOnlineStatusAsync()
        {
            if (Chat?.Participants == null)
                return;

            try
            {
                // Get our user ID
                var currentUserId = _currentUserId > 0 ? _currentUserId : await _chatService.GetCurrentUserIdAsync();
                if (_currentUserId == 0)
                    _currentUserId = currentUserId;

                // Find the other participant
                var otherParticipant = Chat.Participants.FirstOrDefault(p => p.Id != currentUserId);
                if (otherParticipant != null)
                {
                    // If we don't have explicit online status, assume offline
                    // The SignalR service will update if they're actually online
                    if (!otherParticipant.IsOnline)
                    {
                        _logger.LogInformation("Setting participant {UserId} ({Name}) as offline by default",
                            otherParticipant.Id, otherParticipant.DisplayName);
                        otherParticipant.IsOnline = false;
                    }
                }

                // Mark ourselves as online
                var selfParticipant = Chat.Participants.FirstOrDefault(p => p.Id == currentUserId);
                if (selfParticipant != null)
                {
                    selfParticipant.IsOnline = true;
                }

                // Update UI
                OnPropertyChanged(nameof(Chat));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing online status");
            }
        }

        private async Task LoadChatAsync()
        {
            if (string.IsNullOrEmpty(ChatId))
            {
                _logger.LogWarning("Cannot load chat - ChatId is null or empty");
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
                // نشان دادن وضعیت بارگذاری فقط اگر پیامی نمایش داده نشده است
                if (Messages.Count == 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => IsLoading = true);
                }

                // بارگذاری اطلاعات چت
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

                // بارگذاری پیام‌ها (با استفاده از کش در صورت وجود)
                var messages = await _chatService.GetMessagesAsync(chatGuid);
                _logger.LogInformation("Loaded {Count} messages for chat {ChatId}",
                    messages?.Count ?? 0, chatGuid);

                // تبدیل زمان‌های UTC به زمان محلی برای نمایش
                if (messages != null && messages.Count > 0)
                {
                    foreach (var message in messages)
                    {
                        // تنظیم فرمت زمان برای نمایش
                        if (string.IsNullOrEmpty(message.SentAtFormatted))
                        {
                            message.SentAtFormatted = FormatLocalTime(message.LocalSentAt);
                        }
                    }
                }

                // به‌روزرسانی UI در thread اصلی
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // تنظیم چت
                        Chat = chat;

                        // بروزرسانی کالکشن پیام‌ها اگر پیامی بارگذاری شده است
                        if (messages != null && messages.Count > 0)
                        {
                            UpdateMessagesCollection(messages);
                            NoMessages = false;
                            _logger.LogInformation("Added {Count} messages to UI", messages.Count);
                        }
                        else if (messages != null && messages.Count == 0 && Messages.Count == 0)
                        {
                            NoMessages = true;
                            _logger.LogWarning("No messages to display");
                        }

                        // به روزرسانی وضعیت آنلاین
                        Task.Run(() => FixOnlineStatusAsync());

                        // اتمام وضعیت بارگذاری
                        IsLoading = false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing loaded chat data");
                        IsLoading = false;
                    }
                });

                // علامت‌گذاری پیام‌های خوانده نشده به عنوان خوانده شده
                Task.Run(() => MarkUnreadMessagesAsReadAsync());
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
                // Find unread messages that are not sent by the current user
                var unreadMessageIds = Messages
                    .Where(m => !m.IsOwnMessage && !m.IsRead)
                    .Select(m => m.Id)
                    .Where(id => id > 0) // Only include server-assigned IDs
                    .ToList();

                if (unreadMessageIds.Count > 0)
                {
                    _logger.LogInformation("Marking {Count} messages as read", unreadMessageIds.Count);

                    // Mark as read on server
                    await _chatService.MarkAsReadAsync(chatGuid, unreadMessageIds);

                    // Update local message status
                    foreach (var messageId in unreadMessageIds)
                    {
                        var message = Messages.FirstOrDefault(m => m.Id == messageId);
                        if (message != null)
                        {
                            message.IsRead = true;
                            message.ReadAt = DateTime.UtcNow;
                        }
                    }

                    // Update UI
                    await MainThread.InvokeOnMainThreadAsync(() => OnPropertyChanged(nameof(Messages)));
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

            // Clear message input immediately for better UX
            MessageText = string.Empty;

            try
            {
                IsSending = true;

                _logger.LogInformation("Sending message to chat {ChatId}: {MessageText}", chatGuid, messageText);

                // Create temporary message with a unique negative ID
                var tempId = -DateTime.Now.Millisecond - 1000 * new Random().Next(1000, 9999);
                var tempMessage = new MessageModel
                {
                    Id = tempId, // Ensure unique negative ID
                    Content = messageText,
                    SentAt = DateTime.Now, // Use local time for immediate display
                    ChatId = chatGuid,
                    SenderId = _currentUserId > 0 ? _currentUserId : await _chatService.GetCurrentUserIdAsync(),
                    SenderName = "You", // Will be replaced by server response
                    Status = Constants.MessageStatus.Sending,
                    SentAtFormatted = DateTime.Now.ToString("HH:mm"), // Format immediately with local time
                    IsOwnMessage = true // Explicitly set this to true for UI
                };

                // Cache the current user ID for future use
                if (_currentUserId == 0)
                {
                    _currentUserId = tempMessage.SenderId;
                }

                // Add to local messages immediately
                await MainThread.InvokeOnMainThreadAsync(() => {
                    Messages.Add(tempMessage);
                    NoMessages = false;

                    // Also add to chat's messages collection if exists
                    Chat?.Messages?.Add(tempMessage);
                });

                // Send the message through ChatService
                MessageModel serverMessage = null;
                try
                {
                    serverMessage = await _chatService.SendMessageAsync(chatGuid, messageText);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending message to server");
                }

                // Process the response on the UI thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (serverMessage != null)
                    {
                        // Set the server message to match the temp message's isOwnMessage property
                        serverMessage.IsOwnMessage = true;

                        // Keep track of the relationship between temp and server messages
                        _tempToServerMessageIds[tempId] = serverMessage.Id;

                        // Find and replace the temporary message
                        var tempMsg = Messages.FirstOrDefault(m => m.Id == tempId);
                        if (tempMsg != null)
                        {
                            // Update the temp message with the server data
                            var index = Messages.IndexOf(tempMsg);
                            if (index >= 0)
                            {
                                Messages[index] = serverMessage;
                            }

                            // Also update in chat's messages collection if it exists
                            if (Chat?.Messages != null)
                            {
                                var chatTempMsg = Chat.Messages.FirstOrDefault(m => m.Id == tempId);
                                if (chatTempMsg != null)
                                {
                                    var chatIndex = Chat.Messages.IndexOf(chatTempMsg);
                                    if (chatIndex >= 0)
                                    {
                                        Chat.Messages[chatIndex] = serverMessage;
                                    }
                                }
                            }
                        }

                        _logger.LogInformation("Message sent successfully, server ID: {MessageId}", serverMessage.Id);

                        // Invalidate cache since we have a new message
                        MessageCache.InvalidateCache(chatGuid);
                    }
                    else
                    {
                        // Mark the temporary message as failed
                        var tempMsg = Messages.FirstOrDefault(m => m.Id == tempId);
                        if (tempMsg != null)
                        {
                            tempMsg.Status = Constants.MessageStatus.Failed;

                            // Notify UI of the status change
                            OnPropertyChanged(nameof(Messages));
                        }

                        _logger.LogWarning("Failed to send message");
                        _toastService.ShowToastAsync("Failed to send message", ToastType.Error)
                            .ConfigureAwait(false);
                    }

                    IsSending = false;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendMessageAsync: {Message}", ex.Message);
                IsSending = false;
                await _toastService.ShowToastAsync($"Error sending message: {ex.Message}", ToastType.Error);
            }
        }

        // Helper method to format time correctly
        private string FormatLocalTime(DateTime dateTime)
        {
            try
            {
                // اطمینان از استفاده از زمان محلی
                DateTime localTime = dateTime.Kind == DateTimeKind.Utc
                    ? dateTime.ToLocalTime()
                    : dateTime;

                return localTime.ToString("HH:mm");
            }
            catch
            {
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

            await _toastService.ShowToastAsync("نمایش پروفایل در نسخه‌های آینده فعال خواهد شد", ToastType.Info);
        }

        private void OnMessageReceived(MessageModel message)
        {
            if (Chat == null || message.ChatId.ToString() != ChatId)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    _logger.LogInformation("Received message {MessageId} for chat {ChatId} from userId {SenderId}",
                        message.Id, message.ChatId, message.SenderId);

                    // Get our user ID to properly determine if message is our own
                    var currentUserId = _currentUserId > 0 ? _currentUserId : GetUserIdSync();

                    // Set IsOwnMessage based on the sender ID
                    bool isOwnMessage = message.SenderId == currentUserId;
                    message.IsOwnMessage = isOwnMessage;

                    // Check if this message matches any of our temporary messages
                    if (isOwnMessage)
                    {
                        var tempMessage = Messages.FirstOrDefault(m =>
                            m.Id < 0 && // Temp messages have negative IDs
                            m.Content == message.Content &&
                            Math.Abs((m.SentAt - message.SentAt).TotalSeconds) < 60); // Within 60 seconds

                        if (tempMessage != null)
                        {
                            // Found a temporary message, remember this mapping
                            _tempToServerMessageIds[tempMessage.Id] = message.Id;

                            // Just update the temporary message instead of replacing it
                            // This prevents the UI from flickering
                            tempMessage.Id = message.Id;
                            tempMessage.Status = message.Status;
                            tempMessage.SentAt = message.SentAt;
                            tempMessage.SentAtFormatted = message.SentAtFormatted;

                            // Force UI update
                            OnPropertyChanged(nameof(Messages));

                            return; // Handled the temp->server message, no need to proceed
                        }
                    }

                    // Check if this message already exists in our collection by ID
                    var existingMessage = Messages.FirstOrDefault(m => m.Id == message.Id);
                    if (existingMessage != null)
                    {
                        // Just update relevant properties instead of replacing the whole message object
                        existingMessage.Status = message.Status;
                        existingMessage.IsRead = message.IsRead;
                        existingMessage.ReadAt = message.ReadAt;

                        // Force UI update
                        OnPropertyChanged(nameof(Messages));
                    }
                    else
                    {
                        // It's a genuinely new message from someone else (or our own from another device)
                        // Check for exact duplicates
                        bool isDuplicate = Messages.Any(m =>
                            m.Content == message.Content &&
                            Math.Abs((m.SentAt - message.SentAt).TotalSeconds) < 60 &&
                            m.SenderId == message.SenderId);

                        if (!isDuplicate)
                        {
                            // Truly new message, add it
                            Messages.Add(message);
                            NoMessages = false;

                            // Also add to chat's messages
                            Chat?.Messages?.Add(message);

                            // Mark as read immediately if it's not our own
                            if (!isOwnMessage)
                            {
                                message.IsRead = true;
                                message.ReadAt = DateTime.UtcNow;
                                MarkMessageAsReadAsync(message.Id).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling received message");
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

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    _logger.LogInformation("Message {MessageId} in chat {ChatId} marked as read",
                        messageId, chatId);

                    var message = Messages.FirstOrDefault(m => m.Id == messageId);
                    if (message != null)
                    {
                        // به‌روزرسانی وضعیت بدون جایگزینی کامل شیء
                        message.IsRead = true;
                        message.ReadAt = DateTime.UtcNow;
                        message.Status = Constants.MessageStatus.Read;

                        // اعلام تغییر برای به‌روزرسانی UI
                        OnPropertyChanged(nameof(Messages));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message read status update");
                }
            });
        }

        private void OnUserStatusChanged(long userId, bool isOnline, DateTime? lastActive)
        {
            if (Chat?.OtherParticipant?.Id == userId)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        _logger.LogInformation("User {UserId} status changed: Online = {IsOnline}, LastActive = {LastActive}",
                            userId, isOnline, lastActive);

                        Chat.OtherParticipant.IsOnline = isOnline;
                        Chat.OtherParticipant.LastActive = lastActive;

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