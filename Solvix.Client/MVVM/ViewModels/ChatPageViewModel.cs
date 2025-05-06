using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System;
using System.Collections.ObjectModel;


namespace Solvix.Client.MVVM.ViewModels
{
    [QueryProperty(nameof(ChatIdString), "ChatId")]
    public partial class ChatPageViewModel : ObservableObject
    {
        #region Services and Logger
        private readonly IChatService _chatService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        private readonly ILogger<ChatPageViewModel> _logger;
        #endregion

        #region Private Fields
        private long _currentUserId;
        private string? _chatIdString;
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

        #endregion

        #region Public Properties

        public string? ChatIdString
        {
            get => _chatIdString;
            set
            {
                if (_chatIdString == value) return;
                _chatIdString = value;

                if (Guid.TryParse(value, out Guid parsedGuid))
                {
                    if (parsedGuid != ActualChatId)
                    {
                        ActualChatId = parsedGuid;
                        _logger.LogInformation("ChatId received and parsed: {ActualChatId}. Initializing chat.", ActualChatId);
                        MainThread.BeginInvokeOnMainThread(async () => await InitializeChatAsync());
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
            ILogger<ChatPageViewModel> logger)
        {
            _chatService = chatService;
            _toastService = toastService;
            _authService = authService;
            _logger = logger;
        }
        #endregion

        #region Commands

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            var messageContentToSend = NewMessageText;
            if (string.IsNullOrWhiteSpace(messageContentToSend)) return;

            NewMessageText = string.Empty;
            IsSendingMessage = true;

            var optimisticMessage = CreateOptimisticMessage(messageContentToSend);
            Messages.Add(optimisticMessage);
            ScrollToLastMessage();

            try
            {
                var sentMessageDto = await _chatService.SendMessageAsync(ActualChatId, messageContentToSend);
                UpdateSentMessageStatus(optimisticMessage, sentMessageDto);
            }
            catch (Exception ex)
            {
                HandleMessageSendError(ex, optimisticMessage);
            }
            finally
            {
                IsSendingMessage = false;
            }
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

        [RelayCommand]
        private async Task AttachFileAsync() => await _toastService.ShowToastAsync("ارسال فایل (به زودی!)", ToastType.Info);

        [RelayCommand]
        private async Task EmojiAsync() => await _toastService.ShowToastAsync("انتخاب ایموجی (به زودی!)", ToastType.Info);

        [RelayCommand]
        private async Task GoToChatSettingsAsync() => await _toastService.ShowToastAsync("تنظیمات چت (به زودی!)", ToastType.Info);

        #endregion

        #region Private Methods

        private async Task InitializeChatAsync()
        {
            if (ActualChatId == Guid.Empty || IsLoadingMessages) return;

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
                OnPropertyChanged(nameof(CurrentChat)); 

                var initialMessages = await _chatService.GetChatMessagesAsync(ActualChatId, 0, 50);
                if (initialMessages != null)
                {
                    foreach (var msg in initialMessages.OrderBy(m => m.SentAt))
                    {
                        msg.IsOwnMessage = msg.SenderId == _currentUserId;
                        Messages.Add(msg);
                    }
                    _logger.LogInformation("Loaded {Count} initial messages for Chat {ActualChatId}", Messages.Count, ActualChatId);
                    ScrollToLastMessage();
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

        private bool CanSendMessage() => !string.IsNullOrWhiteSpace(NewMessageText) && !IsSendingMessage;

        private MessageModel CreateOptimisticMessage(string content)
        {
            return new MessageModel
            {
                Id = 0,
                ChatId = ActualChatId,
                Content = content,
                SenderId = _currentUserId,
                SentAt = DateTime.Now,
                IsOwnMessage = true,
                Status = Constants.MessageStatus.Sending,
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        private void UpdateSentMessageStatus(MessageModel optimisticMessage, MessageModel? serverMessage)
        {
            var messageToUpdate = Messages.FirstOrDefault(m => m.CorrelationId == optimisticMessage.CorrelationId);
            if (messageToUpdate != null)
            {
                if (serverMessage != null)
                {
                    messageToUpdate.Id = serverMessage.Id;
                    messageToUpdate.Status = Constants.MessageStatus.Sent; 
                    messageToUpdate.SentAt = serverMessage.SentAt;
                    messageToUpdate.CorrelationId = string.Empty;
                    _logger.LogInformation("Optimistic message updated with server info. ID: {Id}", serverMessage.Id);
                }
                else
                {
                    messageToUpdate.Status = Constants.MessageStatus.Failed;
                    _logger.LogWarning("SendMessageAsync returned null, marking optimistic message as Failed. CorrelationId: {CorrId}", optimisticMessage.CorrelationId);
                }
            }
            else { _logger.LogWarning("Could not find optimistic message to update. CorrelationId: {CorrId}", optimisticMessage.CorrelationId); }
        }

        private async void HandleMessageSendError(Exception ex, MessageModel optimisticMessage)
        {
            _logger.LogError(ex, "Error sending message to chat {ActualChatId}", ActualChatId);
            await _toastService.ShowToastAsync("خطا در ارسال پیام.", ToastType.Error);
            var messageToUpdate = Messages.FirstOrDefault(m => m.CorrelationId == optimisticMessage.CorrelationId);
            if (messageToUpdate != null)
            {
                messageToUpdate.Status = Constants.MessageStatus.Failed;
            }
        }

        private void HandleInvalidChatId()
        {
            MainThread.BeginInvokeOnMainThread(async () => {
                await _toastService.ShowToastAsync("خطا: شناسه چت نامعتبر است.", ToastType.Error);
                if (Shell.Current.Navigation.NavigationStack.Count > 1)
                    await Shell.Current.Navigation.PopAsync();
                else
                    await Shell.Current.GoToAsync("..");
            });
        }

        private void ScrollToLastMessage()
        {
            var lastMessage = Messages.LastOrDefault();
            if (lastMessage != null && Messages.Count > 1)
            {
                _logger.LogDebug("Requesting scroll to last message.");
            }
        }
        #endregion

        #region SignalR Handlers (Placeholders)

        public void ReceiveNewMessage(MessageModel message)
        {
            if (message.ChatId == this.ActualChatId && !Messages.Any(m => m.Id == message.Id && m.Id != 0))
            {
                message.IsOwnMessage = message.SenderId == _currentUserId;
                MainThread.BeginInvokeOnMainThread(() => {
                    Messages.Add(message);
                    ScrollToLastMessage();
                });
            }
        }

        public void UpdateMessageStatus(Guid chatId, int messageId, int status)
        {
            if (chatId == this.ActualChatId)
            {
                var message = Messages.FirstOrDefault(m => m.Id == messageId);
                if (message != null && message.IsOwnMessage)
                {
                    MainThread.BeginInvokeOnMainThread(() => {
                        message.Status = status;
                    });
                }
            }
        }
        #endregion
    }
}