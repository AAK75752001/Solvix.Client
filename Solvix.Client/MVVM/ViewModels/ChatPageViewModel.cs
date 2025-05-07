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
        private readonly SemaphoreSlim _initializeSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _loadMessagesCts;
        private Task? _initializationTask;
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

        [ObservableProperty]
        private string _typingIndicatorText = string.Empty;

        [ObservableProperty]
        private bool canLoadMore = true;

        #endregion

        #region Public Properties

        public string? ChatIdString
        {
            get => _chatIdString;
            set
            {
                if (_chatIdString == value) return;
                _chatIdString = value;
                _logger.LogTrace("ChatIdString setter called with value: {Value}", value);

                if (Guid.TryParse(value, out Guid parsedGuid))
                {
                    if (parsedGuid != ActualChatId || !_isInitialized)
                    {
                        _logger.LogInformation("ChatId changed or not initialized. New ChatId: {ParsedGuid}. Previous: {ActualChatId}", parsedGuid, ActualChatId);
                        ActualChatId = parsedGuid;
                        ResetViewModelState();
                        _initializationTask = InitializeChatAsync();
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

            _signalRService.OnMessageReceived += SignalRMessageReceived;
            _signalRService.OnMessageStatusUpdated += SignalRMessageStatusUpdated;
            _signalRService.OnConnectionStateChanged += SignalRConnectionStateChanged;
            _signalRService.OnUserTyping += SignalRUserTyping;
            _signalRService.OnMessageCorrelationConfirmation += SignalRMessageCorrelationConfirmation;

            IsConnected = _signalRService.IsConnected;
        }
        #endregion

        #region Commands

        [RelayCommand]
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

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            if (_isSendingMessage) return;

            var messageContentToSend = NewMessageText?.Trim();
            if (string.IsNullOrWhiteSpace(messageContentToSend) || ActualChatId == Guid.Empty) return;

            _logger.LogInformation("Attempting to send message to chat {ChatId}", ActualChatId);

            // پاک کردن متن پیام قبل از شروع فرآیند ارسال
            string contentCopy = messageContentToSend;
            NewMessageText = string.Empty;

            IsSendingMessage = true;
            var optimisticMessage = CreateOptimisticMessage(contentCopy);

            try
            {
                // افزودن پیام خوش‌بینانه به UI قبل از ارسال واقعی
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Messages.Add(optimisticMessage);
                    ScrollToBottom();
                });
                _logger.LogDebug("Added optimistic message with CorrelationId {CorrId} to UI.", optimisticMessage.CorrelationId);

                bool sentViaSignalR = false;
                if (_signalRService.IsConnected)
                {
                    try
                    {
                        sentViaSignalR = await _signalRService.SendMessageAsync(optimisticMessage);
                        _logger.LogInformation("Attempted to send message via SignalR. Success: {SentViaSignalR}", sentViaSignalR);
                    }
                    catch (Exception sigREx)
                    {
                        _logger.LogError(sigREx, "Error sending message via SignalR.");
                        sentViaSignalR = false;
                    }
                }
                else
                {
                    _logger.LogWarning("SignalR not connected. Will attempt send via API.");
                }

                if (!sentViaSignalR)
                {
                    _logger.LogInformation("Sending message via ChatService API.");
                    var sentMessageDto = await _chatService.SendMessageAsync(ActualChatId, contentCopy);

                    // به‌روزرسانی وضعیت پیام بر اساس پاسخ API
                    await MainThread.InvokeOnMainThreadAsync(() => UpdateSentMessageStatusFromApi(optimisticMessage, sentMessageDto));
                }
            }
            catch (Exception ex)
            {
                HandleMessageSendError(ex, optimisticMessage);
            }
            finally
            {
                IsSendingMessage = false;
                _logger.LogInformation("Finished SendMessageAsync for CorrelationId {CorrId}.", optimisticMessage.CorrelationId);
            }
        }

        [RelayCommand]
        private async Task LoadMoreMessagesAsync()
        {
            if (IsLoadingMessages || !CanLoadMore || ActualChatId == Guid.Empty) return;

            await _loadMessagesSemaphore.WaitAsync();
            try
            {
                if (IsLoadingMessages || !CanLoadMore) return;
                _logger.LogInformation("Attempting to load more messages for chat {ActualChatId}", ActualChatId);
                IsLoadingMessages = true;

                int currentMessageCount = Messages.Count;
                _loadMessagesCts = new CancellationTokenSource();

                var olderMessages = await _chatService.GetChatMessagesAsync(ActualChatId, currentMessageCount, 30);

                if (_loadMessagesCts.IsCancellationRequested) return;

                if (olderMessages != null && olderMessages.Any())
                {
                    if (_currentUserId == 0) _currentUserId = await _authService.GetUserIdAsync();

                    var messagesToInsert = new List<MessageModel>();
                    foreach (var msg in olderMessages.OrderByDescending(m => m.SentAt))
                    {
                        msg.IsOwnMessage = msg.SenderId == _currentUserId;
                        SetMessageStatusFromData(msg); // Set status correctly based on loaded data

                        if (!Messages.Any(m => m.Id > 0 && m.Id == msg.Id)) // Prevent adding duplicates with valid IDs
                        {
                            messagesToInsert.Add(msg);
                        }
                    }

                    if (messagesToInsert.Any())
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            foreach (var msg in messagesToInsert)
                            {
                                Messages.Insert(0, msg); // Insert older messages at the top
                            }
                        });
                        _logger.LogInformation("Loaded {Count} older messages for chat {ActualChatId}", messagesToInsert.Count, ActualChatId);
                        CanLoadMore = olderMessages.Count >= 30;
                    }
                    else
                    {
                        CanLoadMore = false;
                    }
                }
                else
                {
                    CanLoadMore = false;
                }
            }
            catch (OperationCanceledException) { _logger.LogInformation("Load more messages cancelled."); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading more messages.");
                await _toastService.ShowToastAsync("خطا در بارگذاری پیام‌های قبلی.", ToastType.Error);
                CanLoadMore = false;
            }
            finally
            {
                IsLoadingMessages = false;
                _loadMessagesCts?.Dispose();
                _loadMessagesCts = null;
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
        private async Task MarkAllVisibleAsReadAsync()
        {
            if (ActualChatId == Guid.Empty || !Messages.Any()) return;

            try
            {
                // Find messages that are not ours, have a valid ID, and are not yet read
                var messageIdsToMark = Messages
                    .Where(m => !m.IsOwnMessage && m.Id > 0 && m.Status < Constants.MessageStatus.Read)
                    .Select(m => m.Id)
                    .ToList();

                if (!messageIdsToMark.Any()) return;

                _logger.LogInformation("Marking {Count} visible messages as read in chat {ChatId}", messageIdsToMark.Count, ActualChatId);

                // Update UI optimistically
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var msgId in messageIdsToMark)
                    {
                        var msg = Messages.FirstOrDefault(m => m.Id == msgId);
                        if (msg != null)
                        {
                            msg.Status = Constants.MessageStatus.Read;
                            msg.IsRead = true;
                            if (!msg.ReadAt.HasValue) msg.ReadAt = DateTime.UtcNow;
                        }
                    }
                });

                // Send updates to backend via SignalR or API
                if (_signalRService.IsConnected)
                {
                    foreach (var msgId in messageIdsToMark)
                    {
                        try { await _signalRService.MarkAsReadAsync(ActualChatId, msgId); }
                        catch (Exception ex) { _logger.LogError(ex, "Error marking message {MessageId} as read via SignalR", msgId); }
                        await Task.Delay(50); // Small delay
                    }
                }
                else
                {
                    await _chatService.MarkMessagesAsReadAsync(ActualChatId, messageIdsToMark);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read in chat {ChatId}", ActualChatId);
                await _toastService.ShowToastAsync("خطا در بروزرسانی وضعیت خوانده شدن پیام‌ها", ToastType.Error);
            }
        }

        #endregion

        #region SignalR Event Handlers

        private void SignalRMessageReceived(MessageModel message)
        {
            if (_isDisposed || message.ChatId != ActualChatId) return;
            _logger.LogInformation("SignalRMessageReceived: Message Id {MessageId} for Chat {ChatId}", message.Id, message.ChatId);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_currentUserId == 0) _currentUserId = await _authService.GetUserIdAsync();

                message.IsOwnMessage = message.SenderId == _currentUserId;

                // Check if message already exists (by ID or CorrelationId)
                var existingMessage = Messages.FirstOrDefault(m =>
                                        (m.Id > 0 && m.Id == message.Id) ||
                                        (!string.IsNullOrEmpty(m.CorrelationId) && m.CorrelationId == message.CorrelationId));

                if (existingMessage == null)
                {
                    _logger.LogInformation("Adding new message received via SignalR: Id {MessageId}", message.Id);
                    SetMessageStatusFromData(message); // Set initial status based on received data
                    Messages.Add(message);
                    ScrollToBottom(); // Scroll down after adding

                    // Mark as read if it's not our message
                    if (!message.IsOwnMessage)
                    {
                        await MarkReceivedMessageAsReadAsync(message.Id);
                    }
                }
                else
                {
                    // Message already exists, likely confirmation for optimistic message or duplicate receive
                    _logger.LogWarning("Duplicate message received or confirmation for optimistic message via SignalR. Id: {MessageId}, CorrId: {CorrId}", message.Id, message.CorrelationId);

                    // Update existing message if necessary (e.g., confirm ID, update status)
                    if (existingMessage.Id <= 0 && message.Id > 0) existingMessage.Id = message.Id;
                    if (string.IsNullOrEmpty(existingMessage.CorrelationId) && !string.IsNullOrEmpty(message.CorrelationId)) existingMessage.CorrelationId = message.CorrelationId;
                    if (message.SentAt > existingMessage.SentAt) existingMessage.SentAt = message.SentAt; // Use server time

                    SetMessageStatusFromData(message); // Determine the correct status based on received data
                    if (message.Status > existingMessage.Status) // Only update if the new status is higher
                    {
                        existingMessage.Status = message.Status;
                    }
                    existingMessage.IsRead = message.IsRead;
                    existingMessage.ReadAt = message.ReadAt;
                }
            });
        }

        // Handle confirmation event from SignalR
        private void SignalRMessageCorrelationConfirmation(string correlationId, int serverMessageId)
        {
            if (_isDisposed) return;
            _logger.LogInformation("SignalRMessageCorrelationConfirmation: CorrId={CorrelationId}, ServerId={ServerMessageId}", correlationId, serverMessageId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var optimisticMessage = Messages.FirstOrDefault(m => m.CorrelationId == correlationId);
                if (optimisticMessage != null)
                {
                    _logger.LogInformation("Found optimistic message for CorrId {CorrelationId}. Updating Id to {ServerMessageId} and Status to Sent.", correlationId, serverMessageId);
                    optimisticMessage.Id = serverMessageId;
                    if (optimisticMessage.Status == Constants.MessageStatus.Sending)
                    {
                        optimisticMessage.Status = Constants.MessageStatus.Sent; // Mark as Sent upon server confirmation
                    }
                }
                else
                {
                    _logger.LogWarning("Received correlation confirmation for CorrId {CorrelationId}, but no matching optimistic message found.", correlationId);
                }
            });
        }

        private void SignalRMessageStatusUpdated(Guid chatId, int messageId, int status)
        {
            if (_isDisposed || chatId != ActualChatId) return;
            _logger.LogDebug("SignalRMessageStatusUpdated: Chat={ChatId}, MessageId={MessageId}, Status={Status}", chatId, messageId, status);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var message = Messages.FirstOrDefault(m => m.Id == messageId);
                if (message != null)
                {
                    // Update status only if the new status is higher or it's a failure
                    if (status == Constants.MessageStatus.Failed || status > message.Status)
                    {
                        _logger.LogInformation("Updating status for message {MessageId} from {OldStatus} to {NewStatus}", messageId, message.Status, status);
                        message.Status = status;
                        if (status == Constants.MessageStatus.Read)
                        {
                            message.IsRead = true;
                            if (!message.ReadAt.HasValue) message.ReadAt = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Ignoring status update for message {MessageId} from {CurrentStatus} to {NewStatus} (no upgrade or not failure)", messageId, message.Status, status);
                    }
                }
            });
        }

        private void SignalRUserTyping(Guid chatId, long userId, bool isTyping)
        {
            if (_isDisposed || chatId != ActualChatId || userId == _currentUserId) return;
            _logger.LogDebug("SignalRUserTyping: User {UserId} is {IsTyping} in chat {ChatId}", userId, isTyping, chatId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsTyping = isTyping;
                TypingIndicatorText = isTyping ? $"{CurrentChat?.OtherParticipant?.FirstName ?? "User"} در حال نوشتن..." : string.Empty;
            });
        }

        private void SignalRConnectionStateChanged(bool isConnected)
        {
            if (_isDisposed) return;
            _logger.LogInformation("SignalRConnectionStateChanged: IsConnected = {IsConnected}", isConnected);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsConnected = isConnected;
                if (isConnected) { ProcessPendingMessages(); }
            });
        }

        #endregion

        #region Private Methods

        private void ResetViewModelState()
        {
            _initializationTask = null; // Reset the task reference
            _isInitialized = false;
            CurrentChat = null;
            Messages.Clear();
            NewMessageText = string.Empty;
            IsLoadingMessages = false;
            IsSendingMessage = false;
            IsTyping = false;
            TypingIndicatorText = string.Empty;
            CanLoadMore = true;
            _loadMessagesCts?.Cancel(); // Cancel any ongoing message loading
            _logger.LogInformation("ViewModel state reset for new ChatId {ActualChatId}", ActualChatId);
        }

        private async Task InitializeChatAsync()
        {
            await _initializeSemaphore.WaitAsync();
            try
            {
                if (_isInitialized || ActualChatId == Guid.Empty) return;
                _logger.LogInformation("Initializing chat page for ChatId {ActualChatId}", ActualChatId);

                IsLoadingMessages = true;
                Messages.Clear();
                CanLoadMore = true;
                _currentUserId = await _authService.GetUserIdAsync();

                if (_currentUserId == 0) throw new Exception("User ID not found.");

                if (!_signalRService.IsConnected) await _signalRService.StartAsync();

                // دریافت اطلاعات چت
                CurrentChat = await _chatService.GetChatByIdAsync(ActualChatId);
                if (CurrentChat == null) throw new Exception("Chat not found or access denied.");

                // اطمینان از تنظیم صحیح OtherParticipant
                if (!CurrentChat.IsGroup && CurrentChat.Participants != null && CurrentChat.Participants.Any())
                {
                    CurrentChat.OtherParticipant = CurrentChat.Participants.FirstOrDefault(p => p.Id != _currentUserId);
                    _logger.LogInformation("Other participant set: {Name}", CurrentChat.OtherParticipant?.DisplayName ?? "Unknown");
                }

                // دریافت پیام‌ها
                var initialMessages = await _chatService.GetChatMessagesAsync(ActualChatId, 0, 30);
                if (initialMessages != null)
                {
                    foreach (var msg in initialMessages.OrderBy(m => m.SentAt))
                    {
                        msg.IsOwnMessage = msg.SenderId == _currentUserId;
                        SetMessageStatusFromData(msg);
                        Messages.Add(msg);
                    }
                    _logger.LogInformation("Loaded {Count} initial messages for Chat {ActualChatId}", initialMessages.Count, ActualChatId);
                    CanLoadMore = initialMessages.Count >= 30;
                }
                else
                {
                    CanLoadMore = false;
                }

                _isInitialized = true;
                _logger.LogInformation("Chat {ActualChatId} initialized successfully.", ActualChatId);
                ScrollToBottom();

                // علامت‌گذاری پیام‌های قابل مشاهده به عنوان خوانده‌شده
                await MarkAllVisibleAsReadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing chat page for ChatId {ActualChatId}", ActualChatId);
                await _toastService.ShowToastAsync("خطا در بارگذاری چت.", ToastType.Error);
                CanLoadMore = false;
            }
            finally
            {
                IsLoadingMessages = false;
                _initializeSemaphore.Release();
            }
        }

        private bool CanSendMessage() => !string.IsNullOrWhiteSpace(NewMessageText) && !IsSendingMessage && _isInitialized && IsConnected;

        private MessageModel CreateOptimisticMessage(string content)
        {
            return new MessageModel
            {
                Id = 0, // Will be assigned a real ID by the server
                ChatId = ActualChatId,
                Content = content,
                SenderId = _currentUserId,
                SenderName = "Me", // Will be replaced with actual user name
                SentAt = DateTime.UtcNow,
                IsOwnMessage = true,
                Status = Constants.MessageStatus.Sending, // Start with sending status
                CorrelationId = Guid.NewGuid().ToString("N") // Use N format for shorter ID
            };
        }

        // Update based on API response (if SignalR fails or is not used)
        private void UpdateSentMessageStatusFromApi(MessageModel optimisticMessage, MessageModel? serverMessage)
        {
            var messageToUpdate = Messages.FirstOrDefault(m => m.CorrelationId == optimisticMessage.CorrelationId);
            if (messageToUpdate != null)
            {
                if (serverMessage != null && serverMessage.Id > 0)
                {
                    messageToUpdate.Id = serverMessage.Id;
                    messageToUpdate.SentAt = serverMessage.SentAt;
                    SetMessageStatusFromData(serverMessage); // Determine status based on server data
                    messageToUpdate.Status = serverMessage.Status; // Update with server status
                    messageToUpdate.IsRead = serverMessage.IsRead;
                    messageToUpdate.ReadAt = serverMessage.ReadAt;
                    _logger.LogInformation("Optimistic message (CorrId {CorrId}) updated via API. New ID: {Id}, Status: {Status}",
                        optimisticMessage.CorrelationId, serverMessage.Id, serverMessage.Status);
                }
                else
                {
                    messageToUpdate.Status = Constants.MessageStatus.Failed;
                    _logger.LogWarning("SendMessageAsync via API failed or returned invalid data for CorrelationId {CorrId}, marking optimistic message as Failed.",
                        optimisticMessage.CorrelationId);
                }
            }
        }

        private async void HandleMessageSendError(Exception ex, MessageModel optimisticMessage)
        {
            _logger.LogError(ex, "Error sending message (CorrId {CorrId}) to chat {ActualChatId}", optimisticMessage.CorrelationId, ActualChatId);
            await _toastService.ShowToastAsync("خطا در ارسال پیام.", ToastType.Error);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var messageToUpdate = Messages.FirstOrDefault(m => m.CorrelationId == optimisticMessage.CorrelationId);
                if (messageToUpdate != null)
                {
                    messageToUpdate.Status = Constants.MessageStatus.Failed;
                }
            });
        }

        private async void HandleInvalidChatId()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await _toastService.ShowToastAsync("خطا: شناسه چت نامعتبر است.", ToastType.Error);
                try { await Shell.Current.GoToAsync(".."); }
                catch (Exception navEx) { _logger.LogError(navEx, "Navigation failed after invalid ChatId"); }
            });
        }

        public async Task UpdateTypingStatusAsync(bool isTyping)
        {
            if (!IsConnected || ActualChatId == Guid.Empty) return;
            try
            {
                _logger.LogDebug("Sending typing status ({IsTyping}) for chat {ChatId}", isTyping, ActualChatId);
                await _signalRService.TypingAsync(ActualChatId, isTyping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send typing status");
            }
        }

        private async Task MarkReceivedMessageAsReadAsync(int messageId)
        {
            if (messageId <= 0) return;
            _logger.LogInformation("Automatically marking received message {MessageId} as read.", messageId);
            try
            {
                // Prefer SignalR if connected
                if (_signalRService.IsConnected)
                {
                    await _signalRService.MarkAsReadAsync(ActualChatId, messageId);
                }
                else
                {
                    // Fallback to API
                    await _chatService.MarkMessagesAsReadAsync(ActualChatId, new List<int> { messageId });
                }

                // Update local UI state optimistically
                var messageInList = Messages.FirstOrDefault(m => m.Id == messageId);
                if (messageInList != null && !messageInList.IsRead)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        messageInList.IsRead = true;
                        messageInList.ReadAt = DateTime.UtcNow;
                        messageInList.Status = Constants.MessageStatus.Read;
                    });
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error marking received message {MessageId} as read", messageId); }
        }

        // Helper to set initial message status based on loaded data
        private void SetMessageStatusFromData(MessageModel msg)
        {
            if (msg.IsOwnMessage)
            {
                // Logic for own messages
                if (msg.IsRead)
                {
                    msg.Status = Constants.MessageStatus.Read;
                }
                else if (msg.Id > 0)
                {
                    msg.Status = Constants.MessageStatus.Sent; // Default to Sent if we have a server ID
                }
                else
                {
                    msg.Status = Constants.MessageStatus.Sending; // Optimistic message or pending
                }
            }
            else
            {
                // Logic for messages from others
                msg.Status = msg.IsRead ? Constants.MessageStatus.Read : Constants.MessageStatus.Delivered;
            }
        }

        private void ScrollToBottom()
        {
            // Implementation would ideally use a reference to the CollectionView
            // For now we just log since we don't have a direct reference to the UI
            _logger.LogTrace("ScrollToBottom requested (implementation needed in View or using messaging)");
        }

        private async void ProcessPendingMessages()
        {
            // Implementation to resend messages queued while offline
            _logger.LogInformation("Processing any pending messages for chat {ChatId}", ActualChatId);
            // Would typically check for unsent messages and attempt to resend them
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
                _logger.LogInformation("Disposing ChatPageViewModel for ChatId {ChatId}", ActualChatId);
                _isDisposed = true;
                _signalRService.OnMessageReceived -= SignalRMessageReceived;
                _signalRService.OnMessageStatusUpdated -= SignalRMessageStatusUpdated;
                _signalRService.OnConnectionStateChanged -= SignalRConnectionStateChanged;
                _signalRService.OnUserTyping -= SignalRUserTyping;
                _signalRService.OnMessageCorrelationConfirmation -= SignalRMessageCorrelationConfirmation;
                _loadMessagesCts?.Cancel();
                _loadMessagesCts?.Dispose();
                _loadMessagesSemaphore.Dispose();
                _initializeSemaphore.Dispose();
            }
        }
        #endregion
    }
}