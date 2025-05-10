using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;
using Solvix.Client.Core.Helpers;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using Solvix.Client.MVVM.Views;


namespace Solvix.Client.MVVM.ViewModels
{
    [QueryProperty(nameof(ChatIdString), "ChatId")]
    public partial class ChatPageViewModel : ObservableObject, IDisposable
    {
        private readonly IChatService _chatService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        private readonly ISignalRService _signalRService;
        private readonly ILogger<ChatPageViewModel> _logger;

        private long _currentUserId;
        private string? _chatIdString;
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        private readonly SemaphoreSlim _loadMessagesSemaphore = new(1, 1);
        private readonly SemaphoreSlim _initializeSemaphore = new(1, 1);
        private CancellationTokenSource? _loadMessagesCts;
        private Task? _initializationTask;
        private bool _isSignalRSubscribed = false;
        private readonly object _signalRLock = new object();

        [ObservableProperty]
        private ObservableCollection<MessageModel> _messages = new();

        [ObservableProperty]
        private Guid _actualChatId;

        [ObservableProperty]
        private ChatModel? _currentChat;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText = string.Empty;

        [ObservableProperty]
        private bool _isLoadingMessages;

        [ObservableProperty]
        private bool _isLoadingMoreMessages;

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
        private bool _canLoadMore = true;

        public string? ChatIdString
        {
            get => _chatIdString;
            set
            {
                if (_chatIdString == value && _isInitialized && ActualChatId == (Guid.TryParse(value, out Guid g) ? g : Guid.Empty)) return;

                _logger.LogTrace("ChatIdString setter called with value: {Value}. Current ActualChatId: {ActualChatId}, IsInitialized: {IsInitialized}", value, ActualChatId, _isInitialized);

                if (Guid.TryParse(value, out Guid parsedGuid))
                {
                    if (parsedGuid != ActualChatId || !_isInitialized)
                    {
                        _chatIdString = value; // Set _chatIdString here after successful parse and condition check
                        _logger.LogInformation("ChatId changing or not initialized. New ChatId: {ParsedGuid}. Previous: {ActualChatId}", parsedGuid, ActualChatId);
                        ActualChatId = parsedGuid;
                        ResetViewModelStateForNewChat();
                        _initializationTask = InitializeChatAsync();
                    }
                    else if (parsedGuid == ActualChatId && _isInitialized)
                    {
                        _logger.LogInformation("ChatIdString set to the same existing and initialized ChatId {ActualChatId}. No re-initialization needed.", ActualChatId);
                        _chatIdString = value; // Ensure _chatIdString is updated even if no re-init
                    }
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    _chatIdString = value; // Store the invalid value for debugging if needed
                    _logger.LogError("Failed to parse received ChatId string: '{ChatIdString}'", value);
                    HandleInvalidChatId();
                }
                else
                {
                    _chatIdString = null; // Handle null or empty string case
                    _logger.LogInformation("ChatIdString set to null or empty. Resetting state.");
                    ResetViewModelStateForNewChat(); // Or handle as appropriate
                }
            }
        }

        private void ResetViewModelStateForNewChat()
        {
            _isInitialized = false;
            CurrentChat = null;

            if (Application.Current != null && Application.Current.Dispatcher.IsDispatchRequired)
            {
                Application.Current.Dispatcher.Dispatch(() => Messages.Clear());
            }
            else
            {
                Messages.Clear();
            }

            NewMessageText = string.Empty;
            IsLoadingMessages = true;
            IsLoadingMoreMessages = false;
            IsSendingMessage = false;
            IsTyping = false;
            TypingIndicatorText = string.Empty;
            CanLoadMore = true;
            _loadMessagesCts?.Cancel();
            _loadMessagesCts?.Dispose();
            _loadMessagesCts = null;
            _initializationTask = null;
            _logger.LogInformation("ViewModel state reset for potential new chat. ActualChatId: {ActualChatId}", ActualChatId);
        }


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

            SubscribeToSignalREvents();
            IsConnected = _signalRService.IsConnected;
        }


        private void SubscribeToSignalREvents()
        {
            lock (_signalRLock)
            {
                if (_isSignalRSubscribed) return;

                _signalRService.OnMessageReceived += SignalRMessageReceived;
                _signalRService.OnMessageStatusUpdated += SignalRMessageStatusUpdated;
                _signalRService.OnConnectionStateChanged += SignalRConnectionStateChanged;
                _signalRService.OnUserTyping += SignalRUserTyping;
                _signalRService.OnMessageCorrelationConfirmation += SignalRMessageCorrelationConfirmation;

                _isSignalRSubscribed = true;
            }
        }


        private void UnsubscribeFromSignalREvents()
        {
            lock (_signalRLock)
            {
                if (!_isSignalRSubscribed) return;

                _signalRService.OnMessageReceived -= SignalRMessageReceived;
                _signalRService.OnMessageStatusUpdated -= SignalRMessageStatusUpdated;
                _signalRService.OnConnectionStateChanged -= SignalRConnectionStateChanged;
                _signalRService.OnUserTyping -= SignalRUserTyping;
                _signalRService.OnMessageCorrelationConfirmation -= SignalRMessageCorrelationConfirmation;

                _isSignalRSubscribed = false;
            }
        }




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

        private void AddMessageToCollection(MessageModel message, bool insertAtBeginning = false)
        {
            if (message == null) return;

            if (_currentUserId == 0 && message.SenderId != 0)
            {
                _logger.LogWarning("Attempting to add message but _currentUserId is not set. IsOwnMessage might be inaccurate for message from sender {SenderId}", message.SenderId);
            }
            message.IsOwnMessage = message.SenderId == _currentUserId;

            var existingMessage = Messages.FirstOrDefault(m =>
                (!string.IsNullOrEmpty(m.CorrelationId) && !string.IsNullOrEmpty(message.CorrelationId) && m.CorrelationId == message.CorrelationId) ||
                (m.Id > 0 && message.Id > 0 && m.Id == message.Id));

            if (existingMessage != null)
            {
                bool changed = false;
                if (message.Id > 0 && existingMessage.Id <= 0) { existingMessage.Id = message.Id; changed = true; }
                if (existingMessage.Content != message.Content) { existingMessage.Content = message.Content; changed = true; }
                if (message.SentAt > existingMessage.SentAt) { existingMessage.SentAt = message.SentAt; changed = true; }
                if (message.Status != existingMessage.Status) { MessageStatusHelper.UpdateMessageStatus(existingMessage, message.Status, _logger); changed = true; } // Use helper
                if (existingMessage.IsRead != message.IsRead) { existingMessage.IsRead = message.IsRead; changed = true; }
                if (existingMessage.ReadAt != message.ReadAt) { existingMessage.ReadAt = message.ReadAt; changed = true; }

                if (changed) _logger.LogInformation("Updated existing message (ID: {MessageId}, CorrID: {CorrelationId}) in collection.", existingMessage.Id, existingMessage.CorrelationId);
                else _logger.LogTrace("No changes detected for existing message (ID: {MessageId}, CorrID: {CorrelationId}).", existingMessage.Id, existingMessage.CorrelationId);
            }
            else
            {
                if (insertAtBeginning)
                {
                    Messages.Insert(0, message);
                }
                else
                {
                    Messages.Add(message);
                }
                _logger.LogInformation("Added new message (ID: {MessageId}, CorrID: {CorrelationId}, IsOwn: {IsOwn}) to collection.", message.Id, message.CorrelationId, message.IsOwnMessage);

                // به‌روزرسانی آخرین پیام در چت
                if (CurrentChat != null)
                {
                    CurrentChat.LastMessage = message.Content;
                    CurrentChat.LastMessageTime = message.SentAt;
                    _logger.LogDebug("Updated CurrentChat LastMessage and LastMessageTime from new message.");
                }
            }
        }


        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            if (IsSendingMessage) return;

            var messageContentToSend = NewMessageText?.Trim();
            if (string.IsNullOrWhiteSpace(messageContentToSend) || ActualChatId == Guid.Empty || _currentUserId == 0)
            {
                _logger.LogWarning("Cannot send message. Content: {Content}, ChatId: {ChatId}, CurrentUserId: {UserId}",
                    messageContentToSend, ActualChatId, _currentUserId);
                return;
            }

            _logger.LogInformation("Attempting to send message to chat {ChatId}", ActualChatId);

            string contentCopy = messageContentToSend;
            NewMessageText = string.Empty;

            IsSendingMessage = true;
            var optimisticMessage = CreateOptimisticMessage(contentCopy);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AddMessageToCollection(optimisticMessage);
            });
            _logger.LogDebug("Added optimistic message with CorrelationId {CorrId} to UI.", optimisticMessage.CorrelationId);

            bool sentSuccessfully = false;
            try
            {
                if (_signalRService.IsConnected)
                {
                    sentSuccessfully = await _signalRService.SendMessageAsync(optimisticMessage);
                    _logger.LogInformation("Attempted to send message via SignalR. Success: {SentViaSignalR}", sentSuccessfully);
                }

                if (!sentSuccessfully)
                {
                    _logger.LogInformation("SignalR send failed or not connected. Sending message via ChatService API.");
                    var sentMessageDto = await _chatService.SendMessageAsync(ActualChatId, contentCopy);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (sentMessageDto != null)
                        {
                            UpdateSentMessageStatusFromApi(optimisticMessage, sentMessageDto);

                            // Update the chat service cache
                            _chatService.UpdateMessageCache(sentMessageDto);
                            _chatService.UpdateChatCache(ActualChatId, sentMessageDto.Content, sentMessageDto.SentAt);
                        }
                        else
                        {
                            // Remove the failed message from UI
                            Messages.Remove(optimisticMessage);
                        }
                    });

                    sentSuccessfully = sentMessageDto != null && sentMessageDto.Id > 0;
                }

                if (!sentSuccessfully && optimisticMessage.Status != Constants.MessageStatus.Failed)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        var msgInList = Messages.FirstOrDefault(m => m.CorrelationId == optimisticMessage.CorrelationId);
                        if (msgInList != null)
                        {
                            MessageStatusHelper.UpdateMessageStatus(msgInList, Constants.MessageStatus.Failed, _logger);
                            // Remove failed message after a delay
                            Task.Delay(3000).ContinueWith(_ =>
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    Messages.Remove(msgInList);
                                });
                            });
                        }
                    });
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
            if (IsLoadingMoreMessages || !CanLoadMore || ActualChatId == Guid.Empty) return;

            await _loadMessagesSemaphore.WaitAsync();
            try
            {
                if (IsLoadingMoreMessages || !CanLoadMore) return;
                _logger.LogInformation("Loading more messages for chat {ActualChatId}", ActualChatId);
                IsLoadingMoreMessages = true;

                int currentMessageCount = Messages.Count;
                _loadMessagesCts = new CancellationTokenSource();

                var olderMessages = await _chatService.GetChatMessagesAsync(ActualChatId, currentMessageCount, 30);

                if (_loadMessagesCts != null && _loadMessagesCts.IsCancellationRequested) return;

                if (olderMessages != null && olderMessages.Any())
                {
                    if (_currentUserId == 0) _currentUserId = await _authService.GetUserIdAsync();

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var msg in olderMessages.OrderBy(m => m.SentAt))
                        {
                            AddMessageToCollection(msg, true);
                        }
                    });
                    _logger.LogInformation("Loaded {Count} older messages for chat {ActualChatId}", olderMessages.Count, ActualChatId);
                    CanLoadMore = olderMessages.Count >= 30;
                }
                else
                {
                    CanLoadMore = false;
                    _logger.LogInformation("No more older messages to load for chat {ActualChatId}", ActualChatId);
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
                IsLoadingMoreMessages = false;
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
        private async Task MarkAllVisibleAsReadAsync()
        {
            if (ActualChatId == Guid.Empty || !Messages.Any() || _currentUserId == 0) return;

            try
            {
                var messageIdsToMark = Messages
                    .Where(m => !m.IsOwnMessage && m.Id > 0 && m.Status < Constants.MessageStatus.Read)
                    .Select(m => m.Id)
                    .ToList();

                if (!messageIdsToMark.Any()) return;

                _logger.LogInformation("Marking {Count} visible messages as read in chat {ChatId}", messageIdsToMark.Count, ActualChatId);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var msgId in messageIdsToMark)
                    {
                        var msg = Messages.FirstOrDefault(m => m.Id == msgId);
                        if (msg != null)
                        {
                            MessageStatusHelper.UpdateMessageStatus(msg, Constants.MessageStatus.Read, _logger);
                        }
                    }
                });

                if (_signalRService.IsConnected)
                {
                    foreach (var msgId in messageIdsToMark)
                    {
                        try { await _signalRService.MarkAsReadAsync(ActualChatId, msgId); }
                        catch (Exception ex) { _logger.LogError(ex, "Error marking message {MessageId} as read via SignalR", msgId); }
                        await Task.Delay(50);
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

        private void SignalRMessageReceived(MessageModel message)
        {
            if (_isDisposed || message.ChatId != ActualChatId) return;
            _logger.LogInformation("SignalRMessageReceived: Message Id {MessageId} for Chat {ChatId} from Sender {SenderId}",
                message.Id, message.ChatId, message.SenderId);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_currentUserId == 0) _currentUserId = await _authService.GetUserIdAsync();

                SetMessageStatusFromData(message);

                // Check if this is our own message that we already have in the collection
                var existingMessage = Messages.FirstOrDefault(m => m.Id == message.Id ||
                    (!string.IsNullOrEmpty(m.CorrelationId) && m.Content == message.Content && m.SenderId == message.SenderId));

                if (existingMessage != null)
                {
                    // Update existing message rather than adding new one
                    _logger.LogInformation("Found existing message, updating instead of adding. ID: {MessageId}", message.Id);

                    existingMessage.Id = message.Id;
                    existingMessage.Status = message.Status;
                    existingMessage.SentAt = message.SentAt;
                    existingMessage.IsRead = message.IsRead;
                    existingMessage.ReadAt = message.ReadAt;

                    // Update cache
                    _chatService.UpdateMessageCache(existingMessage);
                }
                else
                {
                    // Add new message
                    AddMessageToCollection(message);
                    _chatService.UpdateMessageCache(message);
                }

                if (!message.IsOwnMessage && Shell.Current?.CurrentPage is ChatPage)
                {
                    await MarkReceivedMessageAsReadAsync(message.Id);
                }
            });
        }

        private void SignalRMessageCorrelationConfirmation(string correlationId, int serverMessageId)
        {
            if (_isDisposed) return;
            _logger.LogInformation("SignalRMessageCorrelationConfirmation: CorrId={CorrelationId}, ServerId={ServerMessageId}",
                correlationId, serverMessageId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var optimisticMessage = Messages.FirstOrDefault(m => m.CorrelationId == correlationId);
                if (optimisticMessage != null)
                {
                    _logger.LogInformation("Found optimistic message for CorrId {CorrelationId}. Updating Id to {ServerMessageId} and Status to Sent.",
                        correlationId, serverMessageId);
                    optimisticMessage.Id = serverMessageId;

                    if (optimisticMessage.Status == Constants.MessageStatus.Sending)
                    {
                        MessageStatusHelper.UpdateMessageStatus(optimisticMessage, Constants.MessageStatus.Sent, _logger);
                    }

                    // Update cache
                    _chatService.UpdateMessageCache(optimisticMessage);
                }
                else
                {
                    _logger.LogWarning("Received correlation confirmation for CorrId {CorrelationId}, but no matching optimistic message found.",
                        correlationId);
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
                    MessageStatusHelper.UpdateMessageStatus(message, status, _logger);
                }
            });
        }

        private void SignalRUserTyping(Guid chatId, long userId, bool isTyping)
        {
            if (_isDisposed || chatId != ActualChatId || _currentUserId == 0 || userId == _currentUserId) return;
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

        private async Task InitializeChatAsync()
        {
            if (_isInitialized && _initializationTask != null && !_initializationTask.IsCompleted && ActualChatId != Guid.Empty)
            {
                _logger.LogInformation("Initialization already in progress or completed for ChatId {ActualChatId}", ActualChatId);
                await _initializationTask;
                return;
            }
            if (ActualChatId == Guid.Empty)
            {
                _logger.LogWarning("InitializeChatAsync called with Empty ChatId. Aborting.");
                return;
            }

            await _initializeSemaphore.WaitAsync();
            try
            {
                if (_isInitialized && ActualChatId != Guid.Empty)
                {
                    _logger.LogInformation("Chat {ActualChatId} is already initialized. Skipping.", ActualChatId);
                    return;
                }

                _logger.LogInformation("Initializing chat page for ChatId {ActualChatId}", ActualChatId);

                IsLoadingMessages = true;
                if (Application.Current != null && Application.Current.Dispatcher.IsDispatchRequired)
                {
                    Application.Current.Dispatcher.Dispatch(() => Messages.Clear());
                }
                else
                {
                    Messages.Clear();
                }
                CanLoadMore = true;

                _currentUserId = await _authService.GetUserIdAsync();
                if (_currentUserId == 0)
                {
                    _logger.LogError("User ID not found during chat initialization. Cannot proceed.");
                    await _toastService.ShowToastAsync("خطا در شناسایی کاربر. لطفاً مجدداً وارد شوید.", ToastType.Error);
                    IsLoadingMessages = false;
                    return;
                }

                if (!_signalRService.IsConnected) await _signalRService.StartAsync();

                CurrentChat = await _chatService.GetChatByIdAsync(ActualChatId);
                if (CurrentChat == null)
                {
                    _logger.LogError("Chat not found or access denied for ChatId {ActualChatId}", ActualChatId);
                    await _toastService.ShowToastAsync("چت یافت نشد یا شما دسترسی به این چت را ندارید.", ToastType.Error);
                    IsLoadingMessages = false;
                    return;
                }

                // تنظیم طرف مقابل چت
                if (!CurrentChat.IsGroup && CurrentChat.Participants != null && CurrentChat.Participants.Any())
                {
                    CurrentChat.OtherParticipant = CurrentChat.Participants.FirstOrDefault(p => p.Id != _currentUserId);
                    _logger.LogInformation("Other participant set: {Name}", CurrentChat.OtherParticipant?.DisplayName ?? "Unknown");

                    // اطمینان از داشتن اطلاعات آخرین بازدید
                    if (CurrentChat.OtherParticipant != null)
                    {
                        if (!CurrentChat.OtherParticipant.LastActive.HasValue)
                        {
                            CurrentChat.OtherParticipant.LastActive = DateTime.UtcNow.AddMinutes(-15); // مقدار پیش‌فرض
                            _logger.LogDebug("Set default LastActive value for user {UserId} in chat {ChatId}",
                                CurrentChat.OtherParticipant.Id, ActualChatId);
                        }

                        // بررسی اینکه آیا Initials مقدار دارد
                        _logger.LogDebug("User {UserId} in chat {ChatId} has initials: {Initials}",
                            CurrentChat.OtherParticipant.Id, ActualChatId, CurrentChat.OtherParticipant.Initials);

                        // بررسی اینکه آیا LastActiveText مقدار دارد
                        _logger.LogDebug("User {UserId} in chat {ChatId} has LastActiveText: {LastActiveText}",
                            CurrentChat.OtherParticipant.Id, ActualChatId, CurrentChat.OtherParticipant.LastActiveText);
                    }
                }

                // دریافت پیام‌های اولیه
                var initialMessages = await _chatService.GetChatMessagesAsync(ActualChatId, 0, 30);
                if (initialMessages != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var msg in initialMessages.OrderBy(m => m.SentAt))
                        {
                            AddMessageToCollection(msg);
                        }
                    });
                    _logger.LogInformation("Loaded {Count} initial messages for Chat {ActualChatId}", initialMessages.Count, ActualChatId);
                    CanLoadMore = initialMessages.Count >= 30;
                }
                else
                {
                    CanLoadMore = false;
                    _logger.LogInformation("No initial messages found for Chat {ActualChatId}", ActualChatId);
                }

                _isInitialized = true;
                _logger.LogInformation("Chat {ActualChatId} initialized successfully.", ActualChatId);

                await MarkAllVisibleAsReadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing chat page for ChatId {ActualChatId}", ActualChatId);
                await _toastService.ShowToastAsync("خطا در بارگذاری چت.", ToastType.Error);
                CanLoadMore = false;
                _isInitialized = false; // Ensure it can be re-initialized on error
            }
            finally
            {
                IsLoadingMessages = false;
                _initializeSemaphore.Release();
            }
        }

        private bool CanSendMessage() => !string.IsNullOrWhiteSpace(NewMessageText) && !IsSendingMessage && _isInitialized && IsConnected && _currentUserId != 0;

        private MessageModel CreateOptimisticMessage(string content)
        {
            string senderDisplayName = "Me";
            if (CurrentChat?.Participants != null)
            {
                var currentUserParticipant = CurrentChat.Participants.FirstOrDefault(p => p.Id == _currentUserId);
                if (currentUserParticipant != null && !string.IsNullOrWhiteSpace(currentUserParticipant.DisplayName))
                {
                    senderDisplayName = currentUserParticipant.DisplayName;
                }
            }

            return new MessageModel
            {
                Id = 0,
                ChatId = ActualChatId,
                Content = content,
                SenderId = _currentUserId,
                SenderName = senderDisplayName,
                SentAt = DateTime.UtcNow,
                IsOwnMessage = true,
                Status = Constants.MessageStatus.Sending,
                CorrelationId = Guid.NewGuid().ToString("N")
            };
        }

        private void UpdateSentMessageStatusFromApi(MessageModel optimisticMessage, MessageModel? serverMessage)
        {
            var messageToUpdate = Messages.FirstOrDefault(m => m.CorrelationId == optimisticMessage.CorrelationId);
            if (messageToUpdate != null)
            {
                if (serverMessage != null && serverMessage.Id > 0)
                {
                    messageToUpdate.Id = serverMessage.Id;
                    messageToUpdate.SentAt = serverMessage.SentAt;
                    SetMessageStatusFromData(serverMessage);
                    MessageStatusHelper.UpdateMessageStatus(messageToUpdate, serverMessage.Status, _logger);
                    messageToUpdate.IsRead = serverMessage.IsRead;
                    messageToUpdate.ReadAt = serverMessage.ReadAt;
                    _logger.LogInformation("Optimistic message (CorrId {CorrId}) updated via API. New ID: {Id}, Status: {Status}",
                        optimisticMessage.CorrelationId, serverMessage.Id, serverMessage.Status);
                }
                else
                {
                    MessageStatusHelper.UpdateMessageStatus(messageToUpdate, Constants.MessageStatus.Failed, _logger);
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
                    MessageStatusHelper.UpdateMessageStatus(messageToUpdate, Constants.MessageStatus.Failed, _logger);
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
            if (messageId <= 0 || _currentUserId == 0) return;
            _logger.LogInformation("Automatically marking received message {MessageId} as read by user {UserId}.", messageId, _currentUserId);
            try
            {
                if (_signalRService.IsConnected)
                {
                    await _signalRService.MarkAsReadAsync(ActualChatId, messageId);
                }
                else
                {
                    await _chatService.MarkMessagesAsReadAsync(ActualChatId, new List<int> { messageId });
                }

                var messageInList = Messages.FirstOrDefault(m => m.Id == messageId);
                if (messageInList != null && !messageInList.IsRead)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        MessageStatusHelper.UpdateMessageStatus(messageInList, Constants.MessageStatus.Read, _logger);
                    });
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error marking received message {MessageId} as read", messageId); }
        }

        private void SetMessageStatusFromData(MessageModel msg)
        {
            if (msg.Id == 0 && !string.IsNullOrEmpty(msg.CorrelationId))
            {
                MessageStatusHelper.UpdateMessageStatus(msg, Constants.MessageStatus.Sending, _logger);
                return;
            }

            if (msg.IsOwnMessage)
            {
                if (msg.IsRead) MessageStatusHelper.UpdateMessageStatus(msg, Constants.MessageStatus.Read, _logger);
                else if (msg.Id > 0) MessageStatusHelper.UpdateMessageStatus(msg, Constants.MessageStatus.Sent, _logger);
                else MessageStatusHelper.UpdateMessageStatus(msg, Constants.MessageStatus.Sending, _logger);
            }
            else
            {
                MessageStatusHelper.UpdateMessageStatus(msg, msg.IsRead ? Constants.MessageStatus.Read : Constants.MessageStatus.Delivered, _logger);
            }
        }

        private async void ProcessPendingMessages()
        {
            if (_currentUserId == 0)
            {
                _logger.LogWarning("ProcessPendingMessages: _currentUserId is 0, cannot process pending messages.");
                return;
            }
            _logger.LogInformation("Processing any pending messages for chat {ChatId}", ActualChatId);

            List<MessageModel> pendingMessages;
            lock (Messages) // Ensure thread safety when accessing Messages
            {
                pendingMessages = Messages
                    .Where(m => m.IsOwnMessage &&
                                m.Status == Constants.MessageStatus.Sending &&
                                m.Id == 0 &&
                                !string.IsNullOrEmpty(m.CorrelationId))
                    .ToList();
            }


            if (!pendingMessages.Any())
            {
                _logger.LogInformation("No pending messages to process for chat {ChatId}", ActualChatId);
                return;
            }

            _logger.LogInformation("Found {Count} pending messages to process", pendingMessages.Count);

            foreach (var message in pendingMessages)
            {
                try
                {
                    bool sentViaSignalR = false;
                    if (_signalRService.IsConnected)
                    {
                        sentViaSignalR = await _signalRService.SendMessageAsync(message);
                    }

                    if (!sentViaSignalR)
                    {
                        var sentMessageDto = await _chatService.SendMessageAsync(ActualChatId, message.Content);
                        await MainThread.InvokeOnMainThreadAsync(() => UpdateSentMessageStatusFromApi(message, sentMessageDto));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process pending message (CorrId: {CorrId})", message.CorrelationId);
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        var msgInList = Messages.FirstOrDefault(m => m.CorrelationId == message.CorrelationId);
                        if (msgInList != null) MessageStatusHelper.UpdateMessageStatus(msgInList, Constants.MessageStatus.Failed, _logger);
                    });
                }
            }
        }

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

                // Unsubscribe from SignalR events
                UnsubscribeFromSignalREvents();

                _loadMessagesCts?.Cancel();
                _loadMessagesCts?.Dispose();
                _loadMessagesCts = null;

                _initializeSemaphore.Dispose();
                _loadMessagesSemaphore.Dispose();
            }

            _isDisposed = true;
        }
    }
}