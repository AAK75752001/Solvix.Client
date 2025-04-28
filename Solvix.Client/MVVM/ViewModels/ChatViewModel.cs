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
        private ObservableCollection<MessageModel> _messages = new();

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
                    if (!string.IsNullOrEmpty(_chatId))
                    {
                        _logger.LogInformation("ChatId set to {ChatId}, loading chat...", _chatId);
                        LoadChatAsync().ConfigureAwait(false);
                    }
                }
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
                        Messages = new ObservableCollection<MessageModel>(_chat.Messages);
                    }
                }
            }
        }

        public ObservableCollection<MessageModel> Messages
        {
            get => _messages;
            set
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
                // Show loading indicator on UI thread
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = true);

                // Run data fetching operations in background
                await Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Loading chat with ID: {ChatId}", chatGuid);

                        // First load chat details
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

                        // Then load messages (now using cache if available)
                        var messages = await _chatService.GetMessagesAsync(chatGuid);
                        _logger.LogInformation("Loaded {Count} messages for chat {ChatId}",
                            messages?.Count ?? 0, chatGuid);

                        // Update UI on main thread
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            try
                            {
                                // Set the chat
                                Chat = chat;

                                // Clear any existing messages
                                if (chat.Messages == null)
                                {
                                    chat.Messages = new ObservableCollection<MessageModel>();
                                }
                                else
                                {
                                    chat.Messages.Clear();
                                }

                                // Add loaded messages to the chat
                                if (messages != null && messages.Count > 0)
                                {
                                    foreach (var message in messages)
                                    {
                                        chat.Messages.Add(message);
                                    }

                                    // Update our view model's message collection
                                    Messages = new ObservableCollection<MessageModel>(messages);
                                    NoMessages = false;

                                    _logger.LogInformation("Added {Count} messages to UI", messages.Count);
                                }
                                else
                                {
                                    NoMessages = true;
                                    _logger.LogWarning("No messages to display");
                                }

                                // Set loading to false
                                IsLoading = false;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing loaded chat data");
                                IsLoading = false;
                            }
                        });

                        // Mark unread messages as read in background
                        Task.Run(() => MarkUnreadMessagesAsReadAsync());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background chat loading for {ChatId}", chatGuid);
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            IsLoading = false;
                            _toastService.ShowToastAsync("Error loading chat", ToastType.Error);
                        });
                    }
                });
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
            if (!CanSendMessage || string.IsNullOrEmpty(ChatId) ||
                !Guid.TryParse(ChatId, out var chatGuid))
                return;

            string messageText = MessageText.Trim();

            // Clear message input immediately for better UX
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                MessageText = string.Empty;
                OnPropertyChanged(nameof(MessageText));
            });

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsSending = true);

                _logger.LogInformation("Sending message to chat {ChatId}: {MessageText}",
                    chatGuid, messageText);

                // Create temporary message for immediate display
                var tempMessage = new MessageModel
                {
                    Id = -new Random().Next(1000, 9999), // Temporary negative ID
                    Content = messageText,
                    SentAt = DateTime.UtcNow,
                    ChatId = chatGuid,
                    SenderId = await _chatService.GetCurrentUserIdAsync(),
                    SenderName = "You", // Will be replaced by server response
                    Status = Constants.MessageStatus.Sending,
                    SentAtFormatted = DateTime.UtcNow.ToString("HH:mm"),
                    IsOwnMessage = true // Explicitly set this to true for UI
                };

                // Add to local messages immediately
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // Add to chat messages collection
                    Chat.Messages ??= new ObservableCollection<MessageModel>();
                    Chat.Messages.Add(tempMessage);

                    // Add to view model's messages collection
                    Messages.Add(tempMessage);
                    NoMessages = false;

                    // Force UI update
                    OnPropertyChanged(nameof(Messages));
                });

                // Send the message to the server (in background)
                MessageModel message = null;
                await Task.Run(async () =>
                {
                    try
                    {
                        message = await _chatService.SendMessageAsync(chatGuid, messageText);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background message sending failed");
                    }
                });

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (message != null)
                    {
                        // Ensure the received message has IsOwnMessage=true
                        message.IsOwnMessage = true;

                        // Try to find and remove the temporary message
                        var tempMsg = Messages.FirstOrDefault(m => m.Id == tempMessage.Id);
                        if (tempMsg != null)
                        {
                            Messages.Remove(tempMsg);

                            // Also remove from chat's messages if it exists there
                            if (Chat?.Messages != null)
                            {
                                var chatTempMsg = Chat.Messages.FirstOrDefault(m => m.Id == tempMessage.Id);
                                if (chatTempMsg != null)
                                {
                                    Chat.Messages.Remove(chatTempMsg);
                                }
                            }
                        }

                        // Add the confirmed message from server
                        Messages.Add(message);

                        // Also add to chat's messages
                        Chat?.Messages?.Add(message);

                        _logger.LogInformation("Message sent successfully, server ID: {MessageId}", message.Id);

                        // Invalidate cache since we have new message
                        MessageCache.InvalidateCache(chatGuid);
                    }
                    else
                    {
                        // Mark the temporary message as failed
                        var tempMsg = Messages.FirstOrDefault(m => m.Id == tempMessage.Id);
                        if (tempMsg != null)
                        {
                            tempMsg.Status = Constants.MessageStatus.Failed;
                        }

                        _logger.LogWarning("Failed to send message");
                        _toastService.ShowToastAsync("Failed to send message", ToastType.Error)
                            .ConfigureAwait(false);
                    }

                    IsSending = false;
                    OnPropertyChanged(nameof(Messages));
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsSending = false;
                    _toastService.ShowToastAsync($"Error sending message: {ex.Message}", ToastType.Error);
                });
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
                    _logger.LogInformation("Received message {MessageId} for chat {ChatId}",
                        message.Id, message.ChatId);

                    // Check if this message already exists in our collection
                    var existingMessage = Messages.FirstOrDefault(m => m.Id == message.Id);

                    if (existingMessage != null)
                    {
                        // Replace the existing message
                        var index = Messages.IndexOf(existingMessage);
                        Messages[index] = message;

                        // Also update in chat's messages if it exists there
                        if (Chat?.Messages != null)
                        {
                            var chatExistingMsg = Chat.Messages.FirstOrDefault(m => m.Id == message.Id);
                            if (chatExistingMsg != null)
                            {
                                var chatIndex = Chat.Messages.IndexOf(chatExistingMsg);
                                Chat.Messages[chatIndex] = message;
                            }
                        }
                    }
                    else
                    {
                        // Add the new message
                        Messages.Add(message);
                        NoMessages = false;

                        // Also add to chat's messages
                        if (Chat?.Messages != null)
                        {
                            Chat.Messages.Add(message);
                        }

                        // Mark message as read immediately if it's not from current user
                        if (!message.IsOwnMessage)
                        {
                            MarkUnreadMessagesAsReadAsync().ConfigureAwait(false);
                        }
                    }

                    OnPropertyChanged(nameof(Messages));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling received message");
                }
            });
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
                        message.IsRead = true;
                        message.ReadAt = DateTime.UtcNow;
                        message.Status = Constants.MessageStatus.Read;

                        // Also update in chat's messages if it exists there
                        if (Chat?.Messages != null)
                        {
                            var chatMsg = Chat.Messages.FirstOrDefault(m => m.Id == messageId);
                            if (chatMsg != null)
                            {
                                chatMsg.IsRead = true;
                                chatMsg.ReadAt = DateTime.UtcNow;
                                chatMsg.Status = Constants.MessageStatus.Read;
                            }
                        }

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