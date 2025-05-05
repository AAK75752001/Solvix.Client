using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Solvix.Client.MVVM.ViewModels
{
    [QueryProperty(nameof(ChatIdString), "ChatId")]
    public partial class ChatPageViewModel : ObservableObject
    {
        private readonly IChatService _chatService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        private readonly ILogger<ChatPageViewModel> _logger;

        private long _currentUserId;

        [ObservableProperty]
        private Guid _actualChatId;

        private string? _chatIdString;
        public string? ChatIdString
        {
            get => _chatIdString;
            set
            {
                if (_chatIdString != value)
                {
                    _chatIdString = value;
                    if (Guid.TryParse(value, out Guid parsedGuid))
                    {
                        if (parsedGuid != ActualChatId)
                        {
                            ActualChatId = parsedGuid;
                            _logger.LogInformation("ChatId successfully parsed: {ActualChatId}. Initializing chat.", ActualChatId);
                            MainThread.BeginInvokeOnMainThread(async () => await InitializeChatAsync());
                        }
                    }
                    else if (!string.IsNullOrEmpty(value)) 
                    {
                        _logger.LogError("Failed to parse ChatId string: '{ChatIdString}'", value);
                        MainThread.BeginInvokeOnMainThread(async () => {
                            await _toastService.ShowToastAsync("خطا: شناسه چت نامعتبر است.", ToastType.Error);
                        });
                    }
                }
            }
        }

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

        public ChatPageViewModel(
            IChatService chatService,
            IToastService toastService,
            IAuthService authService,
            ILogger<ChatPageViewModel> logger)
        {
            _chatService = chatService;
            _toastService = toastService;
            _authService = authService;
            _logger = logger;
        }

        private async Task InitializeChatAsync()
        {
            if (ActualChatId == Guid.Empty) return;
            if (IsLoadingMessages) return;

            IsLoadingMessages = true;
            Messages.Clear();
            _currentUserId = await _authService.GetUserIdAsync();

            if (_currentUserId == 0)
            {
                _logger.LogError("InitializeChatAsync failed: Could not get current user ID.");
                await _toastService.ShowToastAsync("خطا در شناسایی کاربر.", ToastType.Error);
                IsLoadingMessages = false;
                return;
            }

            try
            {
                CurrentChat = await _chatService.GetChatByIdAsync(ActualChatId);
                if (CurrentChat != null)
                {
                    OnPropertyChanged(nameof(CurrentChat));
                }
                else
                {
                    _logger.LogWarning("InitializeChatAsync: Could not load chat details for ChatId {ActualChatId}", ActualChatId);
                    await _toastService.ShowToastAsync("اطلاعات چت یافت نشد.", ToastType.Warning);
                }

                var initialMessages = await _chatService.GetChatMessagesAsync(ActualChatId, 0, 50);
                if (initialMessages != null)
                {
                    foreach (var msg in initialMessages.OrderBy(m => m.SentAt))
                    {
                        msg.IsOwnMessage = msg.SenderId == _currentUserId;
                        Messages.Add(msg);
                    }
                    _logger.LogInformation("Loaded {Count} initial messages for Chat {ActualChatId}", Messages.Count, ActualChatId);
                }
                else
                {
                    _logger.LogWarning("InitializeChatAsync: GetChatMessagesAsync returned null for ChatId {ActualChatId}", ActualChatId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing chat page for ChatId {ActualChatId}", ActualChatId);
                await _toastService.ShowToastAsync("خطا در بارگذاری پیام‌ها.", ToastType.Error);
            }
            finally
            {
                IsLoadingMessages = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            var messageContentToSend = NewMessageText;
            NewMessageText = string.Empty;
            IsSendingMessage = true;

            try
            {
                var sentMessageDto = await _chatService.SendMessageAsync(ActualChatId, messageContentToSend);
                if (sentMessageDto != null)
                {
                    _logger.LogInformation("Message sent successfully to chat {ActualChatId}", ActualChatId);
                }
                else
                {
                    _logger.LogWarning("SendMessageAsync returned null for chat {ActualChatId}", ActualChatId);
                    await _toastService.ShowToastAsync("پیام ارسال نشد.", ToastType.Error);
                    NewMessageText = messageContentToSend;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to chat {ActualChatId}", ActualChatId);
                await _toastService.ShowToastAsync("خطا در ارسال پیام.", ToastType.Error);
                NewMessageText = messageContentToSend;
            }
            finally
            {
                IsSendingMessage = false;
            }
        }

        private bool CanSendMessage()
        {
            return !string.IsNullOrWhiteSpace(NewMessageText) && !IsSendingMessage;
        }

        [RelayCommand]
        private async Task LoadMoreMessagesAsync()
        {
            if (IsLoadingMessages || ActualChatId == Guid.Empty) return;

            _logger.LogInformation("Attempting to load more messages for chat {ActualChatId}", ActualChatId);
            IsLoadingMessages = true;
            try
            {
                int currentMessageCount = Messages.Count;
                var olderMessages = await _chatService.GetChatMessagesAsync(ActualChatId, currentMessageCount, 30);

                if (olderMessages != null && olderMessages.Any())
                {
                    _currentUserId = await _authService.GetUserIdAsync();
                    foreach (var msg in olderMessages.OrderByDescending(m => m.SentAt))
                    {
                        msg.IsOwnMessage = msg.SenderId == _currentUserId;
                        Messages.Insert(0, msg);
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
            }
        }

        public void ReceiveNewMessage(MessageModel message)
        {
            if (message.ChatId == this.ActualChatId)
            {
                message.IsOwnMessage = message.SenderId == _currentUserId;
                Messages.Add(message);
            }
        }

        public void UpdateMessageStatus(Guid chatId, int messageId, int status)
        {
            if (chatId == this.ActualChatId)
            {
                var message = Messages.FirstOrDefault(m => m.Id == messageId);
                if (message != null && message.IsOwnMessage)
                {
                    message.Status = status;
                }
            }
        }
    }
}