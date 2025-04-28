using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Solvix.Client.Helpers;


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
        private bool _isFirstLoad = true;
        private int _loadAttempts = 0;
        private const int MaxLoadAttempts = 3;

        public string ChatId
        {
            get => _chatId;
            set
            {
                if (_chatId != value)
                {
                    _chatId = value;
                    OnPropertyChanged();

                    // Reset loading state when chat ID changes
                    _isFirstLoad = true;
                    _loadAttempts = 0;

                    // Si ya hemos recibido un valor, cargar el chat
                    if (!string.IsNullOrEmpty(_chatId))
                    {
                        _logger.LogInformation("ChatId set to {ChatId}, loading chat...", _chatId);
                        LoadChatAsync().ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                _logger.LogError(t.Exception, "Error in LoadChatAsync continuation");
                            }
                        });
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
                    OnPropertyChanged(nameof(Messages));
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

        public bool CanSendMessage => !string.IsNullOrWhiteSpace(MessageText) && !IsSending;

        public ObservableCollection<MessageModel> Messages => Chat?.Messages ?? new ObservableCollection<MessageModel>();

        public ICommand SendMessageCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ViewProfileCommand { get; }

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

            // Subscribe to SignalR events
            _signalRService.OnMessageReceived += OnMessageReceived;
            _signalRService.OnMessageRead += OnMessageRead;
            _signalRService.OnUserStatusChanged += OnUserStatusChanged;
        }

        private async Task LoadChatAsync()
        {
            if (string.IsNullOrEmpty(ChatId))
            {
                _logger.LogWarning("ChatId is empty, cannot load chat");
                return;
            }

            if (!Guid.TryParse(ChatId, out var chatGuid))
            {
                _logger.LogError("Invalid ChatId format: {ChatId}", ChatId);
                await _toastService.ShowToastAsync("Invalid chat ID format", ToastType.Error);
                await GoBackAsync();
                return;
            }

            try
            {
                _logger.LogInformation("Loading chat with ID: {ChatId}, attempt: {Attempt}", chatGuid, _loadAttempts + 1);

                // Show loading indicator
                IsLoading = true;

                // Try to get the chat with messages
                ChatModel chat = null;
                List<MessageModel> messages = null;

                try
                {
                    // First, try to get the chat data
                    chat = await _chatService.GetChatAsync(chatGuid);

                    if (chat != null)
                    {
                        _logger.LogInformation("Successfully loaded chat: {ChatId}", chatGuid);

                        // Ensure the Messages collection is initialized
                        if (chat.Messages == null)
                        {
                            chat.Messages = new ObservableCollection<MessageModel>();
                        }
                    }
                    else
                    {
                        _logger.LogWarning("GetChatAsync returned null for {ChatId}", chatGuid);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading chat {ChatId}", chatGuid);
                    await _toastService.ShowToastAsync($"Error loading chat: {ex.Message}", ToastType.Error);
                }

                // Even if the chat data failed to load, try to load messages separately
                try
                {
                    messages = await _chatService.GetMessagesAsync(chatGuid);
                    _logger.LogInformation("Loaded {Count} messages for chat {ChatId}",
                        messages?.Count ?? 0, chatGuid);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading messages for chat {ChatId}", chatGuid);

                    // Only show toast if this is the first attempt
                    if (_isFirstLoad)
                    {
                        await _toastService.ShowToastAsync($"Error loading messages: {ex.Message}", ToastType.Error);
                    }
                }

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    // If we have the chat data, set it
                    if (chat != null)
                    {
                        Chat = chat;

                        // If we also have messages, add them to the chat
                        if (messages != null && messages.Count > 0)
                        {
                            Chat.Messages.Clear();
                            foreach (var message in messages)
                            {
                                Chat.Messages.Add(message);
                            }
                            _logger.LogInformation("Added {Count} messages to chat view", messages.Count);
                        }

                        // Mark unread messages as read
                        Task.Run(async () => await MarkUnreadMessagesAsReadAsync());
                    }
                    else if (Chat == null && _loadAttempts < MaxLoadAttempts)
                    {
                        // If we couldn't load the chat but have messages, create a minimal chat object
                        if (messages != null && messages.Count > 0)
                        {
                            var tempChat = new ChatModel
                            {
                                Id = chatGuid,
                                Messages = new ObservableCollection<MessageModel>(messages),
                                Participants = new List<UserModel>(),
                                Title = "Loading...", // Temporary title
                            };

                            Chat = tempChat;
                            _logger.LogInformation("Created temporary chat with {Count} messages", messages.Count);

                            // Retry loading the full chat data later
                            _loadAttempts++;
                            Task.Run(async () =>
                            {
                                await Task.Delay(2000); // Wait 2 seconds before retrying
                                await LoadChatAsync();
                            });
                        }
                        else if (_isFirstLoad)
                        {
                            // If this is the first load attempt and we have no data, show error
                            await _toastService.ShowToastAsync("Could not load chat data", ToastType.Error);
                        }
                    }
                });

                _isFirstLoad = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error loading chat {ChatId}", chatGuid);
                await _toastService.ShowToastAsync($"Error: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task MarkUnreadMessagesAsReadAsync()
        {
            if (Chat == null || string.IsNullOrEmpty(ChatId) || !Guid.TryParse(ChatId, out var chatGuid))
                return;

            try
            {
                // Find unread messages that are not from the current user
                var unreadMessageIds = Chat.Messages
                    .Where(m => !m.IsOwnMessage && !m.IsRead)
                    .Select(m => m.Id)
                    .ToList();

                if (unreadMessageIds.Count > 0)
                {
                    _logger.LogInformation("Marking {Count} messages as read", unreadMessageIds.Count);

                    // Mark as read on the server
                    await _chatService.MarkAsReadAsync(chatGuid, unreadMessageIds);

                    // Update local message status
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var messageId in unreadMessageIds)
                        {
                            var message = Chat.Messages.FirstOrDefault(m => m.Id == messageId);
                            if (message != null)
                            {
                                message.IsRead = true;
                                message.ReadAt = DateTime.UtcNow;
                            }
                        }
                        OnPropertyChanged(nameof(Messages));
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
            if (!CanSendMessage || string.IsNullOrEmpty(ChatId) ||
                !Guid.TryParse(ChatId, out var chatGuid))
                return;

            var messageContent = MessageText;
            MessageText = string.Empty;

            try
            {
                IsSending = true;

                // Create a temporary message to show in the UI immediately
                var tempMessage = new MessageModel
                {
                    Id = -new Random().Next(1000, 9999), // Temporary negative ID
                    Content = messageContent,
                    SentAt = DateTime.UtcNow,
                    ChatId = chatGuid,
                    Status = Constants.MessageStatus.Sending
                };

                // Add to UI
                Chat.Messages.Add(tempMessage);
                OnPropertyChanged(nameof(Messages));

                // Send the message
                var message = await _chatService.SendMessageAsync(chatGuid, messageContent);

                if (message != null)
                {
                    // Update or replace the temporary message
                    var index = Chat.Messages.IndexOf(tempMessage);
                    if (index >= 0)
                    {
                        Chat.Messages[index] = message;
                        OnPropertyChanged(nameof(Messages));
                    }
                }
                else
                {
                    // Mark the temporary message as failed
                    tempMessage.Status = Constants.MessageStatus.Failed;
                    OnPropertyChanged(nameof(Messages));
                    await _toastService.ShowToastAsync("Failed to send message", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message: {Message}", ex.Message);
                await _toastService.ShowToastAsync($"Error: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsSending = false;
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
                _logger.LogError(ex, "Error navigating back from chat page");
            }
        }

        private async Task ViewProfileAsync()
        {
            if (Chat?.OtherParticipant == null)
                return;

            await _toastService.ShowToastAsync("Profile view will be available in a future update", ToastType.Info);
        }

        private void OnMessageReceived(MessageModel message)
        {
            if (Chat == null || !string.IsNullOrEmpty(ChatId) && message.ChatId.ToString() != ChatId)
                return;

            // Add or update the message in the chat
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Check if we already have this message
                var existingMessage = Chat.Messages.FirstOrDefault(m => m.Id == message.Id);

                if (existingMessage != null)
                {
                    // Update existing message
                    var index = Chat.Messages.IndexOf(existingMessage);
                    Chat.Messages[index] = message;
                }
                else
                {
                    // Add new message
                    Chat.Messages.Add(message);

                    // Mark as read immediately if it's not our own message
                    if (!message.IsOwnMessage)
                    {
                        Task.Run(async () => await MarkUnreadMessagesAsReadAsync());
                    }
                }

                OnPropertyChanged(nameof(Messages));
            });
        }

        private void OnMessageRead(Guid chatId, int messageId)
        {
            if (Chat == null || string.IsNullOrEmpty(ChatId) || chatId.ToString() != ChatId)
                return;

            // Find the message and update its read status
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var message = Chat.Messages.FirstOrDefault(m => m.Id == messageId);

                if (message != null)
                {
                    message.IsRead = true;
                    message.ReadAt = DateTime.UtcNow;
                    message.Status = Constants.MessageStatus.Read;

                    // Refresh the message in the collection to update the UI
                    OnPropertyChanged(nameof(Messages));
                }
            });
        }

        private void OnUserStatusChanged(long userId, bool isOnline, DateTime? lastActive)
        {
            if (Chat?.OtherParticipant?.Id == userId)
            {
                // Update other participant's online status
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Chat.OtherParticipant.IsOnline = isOnline;
                    Chat.OtherParticipant.LastActive = lastActive;
                    OnPropertyChanged(nameof(Chat));
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