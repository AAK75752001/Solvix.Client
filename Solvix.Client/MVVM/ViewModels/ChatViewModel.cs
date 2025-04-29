using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

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

        // Message tracking for deduplication
        private readonly HashSet<string> _messageSignatures = new();
        private readonly SemaphoreSlim _messageProcessingLock = new SemaphoreSlim(1, 1);

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

        private async Task<long> GetUserIdAsync()
        {
            try
            {
                // Try to get from field first
                if (_currentUserId > 0)
                    return _currentUserId;

                // Try to get from chat service
                _currentUserId = await _chatService.GetCurrentUserIdAsync();
                return _currentUserId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user ID");
                return 0;
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

        // Generate a unique signature for a message to aid in deduplication
        private string GenerateMessageSignature(MessageModel message)
        {
            return $"{message.SenderId}:{message.Content}:{message.SentAt.Ticks}";
        }

        // Smoothly update the messages collection without replacing it completely
        private async Task UpdateMessagesCollection(List<MessageModel> newMessages)
        {
            if (newMessages == null || newMessages.Count == 0)
            {
                // Only clear if there are messages in the collection
                if (Messages.Count > 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        Messages.Clear();
                        _messageSignatures.Clear();
                        NoMessages = true;
                    });
                }
                return;
            }

            await _messageProcessingLock.WaitAsync();

            try
            {
                // Update NoMessages based on new message count
                NoMessages = newMessages.Count == 0;

                await MainThread.InvokeOnMainThreadAsync(() => {
                    // Process each new message
                    foreach (var newMessage in newMessages.OrderBy(m => m.SentAt))
                    {
                        // Check if this message is already in our collection
                        var signature = GenerateMessageSignature(newMessage);
                        if (_messageSignatures.Contains(signature))
                        {
                            // Message already exists, just update properties
                            var existingMessage = Messages.FirstOrDefault(m =>
                                m.SenderId == newMessage.SenderId &&
                                m.Content == newMessage.Content &&
                                Math.Abs((m.SentAt - newMessage.SentAt).TotalSeconds) < 60);

                            if (existingMessage != null)
                            {
                                // Update status, read state, etc.
                                existingMessage.Status = newMessage.Status;
                                existingMessage.IsRead = newMessage.IsRead;
                                existingMessage.ReadAt = newMessage.ReadAt;
                            }
                            continue;
                        }

                        // Ensure the message has correct IsOwnMessage property
                        newMessage.IsOwnMessage = newMessage.SenderId == _currentUserId;

                        // Add the message and its signature
                        Messages.Add(newMessage);
                        _messageSignatures.Add(signature);
                    }

                    // Ensure messages are sorted by time
                    var sortedMessages = Messages.OrderBy(m => m.SentAt).ToList();

                    // Only replace if order has changed
                    bool orderChanged = false;
                    for (int i = 0; i < Messages.Count; i++)
                    {
                        if (i < sortedMessages.Count && !object.ReferenceEquals(Messages[i], sortedMessages[i]))
                        {
                            orderChanged = true;
                            break;
                        }
                    }

                    if (orderChanged)
                    {
                        Messages.Clear();
                        foreach (var message in sortedMessages)
                        {
                            Messages.Add(message);
                        }
                    }
                });
            }
            finally
            {
                _messageProcessingLock.Release();
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
                // Only show loading if there are no messages
                if (Messages.Count == 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => IsLoading = true);
                }

                // Load chat data
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

                // Load messages (using cache if available)
                var messages = await _chatService.GetMessagesAsync(chatGuid);
                _logger.LogInformation("Loaded {Count} messages for chat {ChatId}",
                    messages?.Count ?? 0, chatGuid);

                // Ensure user ID is loaded
                if (_currentUserId == 0)
                {
                    _currentUserId = await GetUserIdAsync();
                }

                // Format message times for display
                if (messages != null && messages.Count > 0)
                {
                    foreach (var message in messages)
                    {
                        // Make sure each message has its IsOwnMessage property set correctly
                        message.IsOwnMessage = message.SenderId == _currentUserId;

                        // Format time for display
                        if (string.IsNullOrEmpty(message.SentAtFormatted))
                        {
                            message.SentAtFormatted = FormatLocalTime(message.LocalSentAt);
                        }
                    }
                }

                // Update UI on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // Set chat
                        Chat = chat;

                        // Update messages if any were loaded
                        if (messages != null && messages.Count > 0)
                        {
                            UpdateMessagesCollection(messages).ConfigureAwait(false);
                            NoMessages = false;
                            _logger.LogInformation("Added {Count} messages to UI", messages.Count);
                        }
                        else if (messages != null && messages.Count == 0 && Messages.Count == 0)
                        {
                            NoMessages = true;
                            _logger.LogWarning("No messages to display");
                        }

                        // Fix online status
                        Task.Run(() => FixOnlineStatusAsync());

                        // Done loading
                        IsLoading = false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing loaded chat data");
                        IsLoading = false;
                    }
                });

                // Mark unread messages as read
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
                await _messageProcessingLock.WaitAsync();

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
                finally
                {
                    _messageProcessingLock.Release();
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
                await _messageProcessingLock.WaitAsync();

                try
                {
                    IsSending = true;

                    _logger.LogInformation("Sending message to chat {ChatId}: {MessageText}", chatGuid, messageText);

                    // Get current user ID if not already set
                    if (_currentUserId == 0)
                    {
                        _currentUserId = await GetUserIdAsync();
                    }

                    // Create temporary message with a unique negative ID
                    var tempId = -DateTime.Now.Millisecond - 1000 * new Random().Next(1000, 9999);
                    var tempMessage = new MessageModel
                    {
                        Id = tempId,
                        Content = messageText,
                        SentAt = DateTime.Now, // Use local time for immediate display
                        ChatId = chatGuid,
                        SenderId = _currentUserId,
                        SenderName = "You", // Will be replaced by server response
                        Status = Constants.MessageStatus.Sending,
                        SentAtFormatted = DateTime.Now.ToString("HH:mm"),
                        IsOwnMessage = true // Explicitly set for UI
                    };

                    // Generate signature for this message
                    var signature = GenerateMessageSignature(tempMessage);
                    _messageSignatures.Add(signature);

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

                            // Add server message signature to avoid duplication when receiving via SignalR
                            var serverSignature = GenerateMessageSignature(serverMessage);
                            _messageSignatures.Add(serverSignature);

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
                finally
                {
                    _messageProcessingLock.Release();
                }
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
                // Ensure we use local time
                DateTime localTime = dateTime.Kind == DateTimeKind.Utc
                    ? dateTime.ToLocalTime()
                    : dateTime;

                return localTime.ToString("HH:mm");
            }
            catch
            {
                // Fallback to simple format in case of error
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

            Task.Run(async () => {
                await _messageProcessingLock.WaitAsync();

                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
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

                            // Generate message signature for deduplication
                            var signature = GenerateMessageSignature(message);

                            // Check if this is a duplicate
                            if (_messageSignatures.Contains(signature))
                            {
                                _logger.LogDebug("Ignoring duplicate message: {Signature}", signature);

                                // Just update status for existing message
                                var existingMessage = Messages.FirstOrDefault(m =>
                                    m.SenderId == message.SenderId &&
                                    m.Content == message.Content &&
                                    Math.Abs((m.SentAt - message.SentAt).TotalSeconds) < 60);

                                if (existingMessage != null)
                                {
                                    // Update status, read state, etc.
                                    existingMessage.Status = message.Status;
                                    existingMessage.IsRead = message.IsRead;
                                    existingMessage.ReadAt = message.ReadAt;

                                    // Force UI update
                                    OnPropertyChanged(nameof(Messages));
                                }

                                return;
                            }

                            // This is a new message we haven't seen before
                            _messageSignatures.Add(signature);

                            // If this is an own message, it might match a temporary message
                            if (isOwnMessage)
                            {
                                var tempMessage = Messages.FirstOrDefault(m =>
                                    m.Id < 0 && // Temp messages have negative IDs
                                    m.Content == message.Content &&
                                    Math.Abs((m.SentAt - message.SentAt).TotalSeconds) < 60); // Within 60 seconds

                                if (tempMessage != null)
                                {
                                    // Found a temporary message, replace it with server data
                                    _tempToServerMessageIds[tempMessage.Id] = message.Id;

                                    tempMessage.Id = message.Id;
                                    tempMessage.Status = message.Status;
                                    tempMessage.SentAt = message.SentAt;
                                    tempMessage.SentAtFormatted = message.SentAtFormatted;

                                    // Force UI update
                                    OnPropertyChanged(nameof(Messages));
                                    return; // Handled the temp->server message, no need to add
                                }
                            }

                            // Genuinely new message
                            Messages.Add(message);
                            NoMessages = false;

                            // Also add to chat's messages collection
                            Chat?.Messages?.Add(message);

                            // Sort messages by time
                            var sortedMessages = Messages.OrderBy(m => m.SentAt).ToList();
                            Messages.Clear();
                            foreach (var msg in sortedMessages)
                            {
                                Messages.Add(msg);
                            }

                            // Mark as read immediately if it's not our own
                            if (!isOwnMessage)
                            {
                                message.IsRead = true;
                                message.ReadAt = DateTime.UtcNow;
                                MarkMessageAsReadAsync(message.Id).ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error handling received message");
                        }
                    });
                }
                finally
                {
                    _messageProcessingLock.Release();
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

            Task.Run(async () => {
                await _messageProcessingLock.WaitAsync();

                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            _logger.LogInformation("Message {MessageId} in chat {ChatId} marked as read",
                                messageId, chatId);

                            var message = Messages.FirstOrDefault(m => m.Id == messageId);
                            if (message != null)
                            {
                                // Update status without replacing the entire object
                                message.IsRead = true;
                                message.ReadAt = DateTime.UtcNow;
                                message.Status = Constants.MessageStatus.Read;

                                // Notify UI of change
                                OnPropertyChanged(nameof(Messages));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error handling message read status update");
                        }
                    });
                }
                finally
                {
                    _messageProcessingLock.Release();
                }
            });
        }

        private void OnUserStatusChanged(long userId, bool isOnline, DateTime? lastActive)
        {
            if (Chat?.OtherParticipant?.Id == userId)
            {
                MainThread.BeginInvokeOnMainThreadAsync(() =>
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