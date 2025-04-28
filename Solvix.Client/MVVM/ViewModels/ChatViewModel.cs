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

        public string ChatId
        {
            get => _chatId;
            set
            {
                if (_chatId != value)
                {
                    _chatId = value;
                    OnPropertyChanged();

                    // Si ya hemos recibido un valor, cargar el chat
                    if (!string.IsNullOrEmpty(_chatId))
                    {
                        _logger.LogInformation("ChatId set to {ChatId}, loading chat...", _chatId);
                        LoadChatAsync().ContinueWith(t => {
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
                _logger.LogInformation("Loading chat with ID: {ChatId}", chatGuid);
                IsLoading = true;

                // Obtener el chat primero
                var chat = await _chatService.GetChatAsync(chatGuid);

                if (chat != null)
                {
                    // Asignar el chat al modelo
                    Chat = chat;

                    // Asegurarse que Messages está inicializado
                    if (Chat.Messages == null)
                    {
                        Chat.Messages = new ObservableCollection<MessageModel>();
                    }

                    // Explícitamente cargar los mensajes
                    var messages = await _chatService.GetMessagesAsync(chatGuid);

                    // Limpiar mensajes existentes y añadir los nuevos
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        Chat.Messages.Clear();
                        foreach (var message in messages)
                        {
                            Chat.Messages.Add(message);
                        }

                        _logger.LogInformation("Added {Count} messages to chat", messages.Count);
                        OnPropertyChanged(nameof(Messages));
                    });

                    // Marcar mensajes como leídos
                    Task.Run(async () => await MarkUnreadMessagesAsReadAsync());
                }
                else
                {
                    _logger.LogWarning("GetChatAsync returned null for {ChatId}", chatGuid);
                    await _toastService.ShowToastAsync("Could not load chat", ToastType.Error);
                    await GoBackAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat {ChatId}", chatGuid);
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
                    await MainThread.InvokeOnMainThreadAsync(() => {
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