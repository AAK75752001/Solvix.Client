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
    [QueryProperty(nameof(ChatId), "ChatId")]
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

        public ObservableCollection<MessageModel> Messages
        {
            get
            {
                if (Chat?.Messages == null)
                {
                    return new ObservableCollection<MessageModel>();
                }
                return Chat.Messages;
            }
        }
        public ICommand SendMessageCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ViewProfileCommand { get; }

        public ChatViewModel(IChatService chatService, ISignalRService signalRService, IToastService toastService)
        {
            _chatService = chatService;
            _signalRService = signalRService;
            _toastService = toastService;

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
            if (string.IsNullOrEmpty(ChatId) || !Guid.TryParse(ChatId, out var chatGuid))
                return;

            try
            {
                IsLoading = true;

                var chat = await _chatService.GetChatAsync(chatGuid);

                if (chat != null)
                {
                    Chat = chat;

                    // Marcar los mensajes como leídos en segundo plano
                    Task.Run(async () =>
                    {
                        try
                        {
                            await MarkUnreadMessagesAsReadAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error marking messages as read");
                        }
                    });
                }
                else
                {
                    await _toastService.ShowToastAsync("Failed to load chat", ToastType.Error);
                    await GoBackAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat {ChatId}: {Message}", ChatId, ex.Message);
                await _toastService.ShowToastAsync($"Error: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SendMessageAsync()
        {
            if (!CanSendMessage || !Guid.TryParse(ChatId, out var chatGuid))
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

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Chat.Messages.Add(tempMessage);
                });

                // Send the message
                var message = await _chatService.SendMessageAsync(chatGuid, messageContent);

                if (message != null)
                {
                    // Update or replace the temporary message
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var index = Chat.Messages.IndexOf(tempMessage);
                        if (index >= 0)
                        {
                            Chat.Messages[index] = message;
                        }
                    });
                }
                else
                {
                    // Mark the temporary message as failed
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        tempMessage.Status = Constants.MessageStatus.Failed;
                        var index = Chat.Messages.IndexOf(tempMessage);
                        if (index >= 0)
                        {
                            Chat.Messages[index] = tempMessage;
                        }
                    });

                    await _toastService.ShowToastAsync("Failed to send message", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Error: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsSending = false;
            }
        }

        private async Task MarkUnreadMessagesAsReadAsync()
        {
            if (Chat == null || !Guid.TryParse(ChatId, out var chatGuid))
                return;

            try
            {
                // Encontrar mensajes no leídos que no son del usuario actual
                var unreadMessageIds = Chat.Messages
                    .Where(m => !m.IsOwnMessage && !m.IsRead)
                    .Select(m => m.Id)
                    .ToList();

                if (unreadMessageIds.Count > 0)
                {
                    // Marcar como leídos
                    await _chatService.MarkAsReadAsync(chatGuid, unreadMessageIds);

                    // Actualizar localmente
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        foreach (var message in Chat.Messages)
                        {
                            if (unreadMessageIds.Contains(message.Id))
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
                // No mostrar toast para no interrumpir la experiencia
            }
        }

        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        private async Task ViewProfileAsync()
        {
            if (Chat?.OtherParticipant == null)
                return;

            await _toastService.ShowToastAsync("Profile view will be available in a future update", ToastType.Info);
        }

        private void OnMessageReceived(MessageModel message)
        {
            if (Chat == null || message.ChatId != Guid.Parse(ChatId))
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
                        MarkUnreadMessagesAsReadAsync().ConfigureAwait(false);
                    }
                }
            });
        }

        private void OnMessageRead(Guid chatId, int messageId)
        {
            if (Chat == null || chatId != Guid.Parse(ChatId))
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
                    var index = Chat.Messages.IndexOf(message);
                    if (index >= 0)
                    {
                        Chat.Messages[index] = message;
                    }
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