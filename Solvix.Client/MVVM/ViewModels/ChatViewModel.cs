using System.Collections.Concurrent;
using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Helpers;
using CollectionExtensions = Solvix.Client.Core.Helpers.CollectionExtensions;

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
        private ObservableCollection<MessageModel> _messages = new ObservableCollection<MessageModel>();
        private bool _isInitialized = false;
        private bool _isLoadingMore;
        private int _messagesSkip = 0;
        private const int MessagesPageSize = 30;
        private bool _isFirstLoad = true;


        // دیکشنری برای پیگیری پیام‌های موقت با استفاده از ConcurrentDictionary برای امنیت چندنخی
        // کلید: ID موقت (منفی)، مقدار: CorrelationId
        private readonly ConcurrentDictionary<int, string> _pendingMessagesById = new();

        // کلید: CorrelationId، مقدار: ID موقت
        private readonly ConcurrentDictionary<string, int> _pendingMessagesByCorrelation = new();

        public string ChatId
        {
            get => _chatId;
            set
            {
                if (_chatId != value)
                {
                    _chatId = value;
                    OnPropertyChanged();

                    if (!string.IsNullOrEmpty(_chatId) && !_isInitialized)
                    {
                        _isInitialized = true;
                        LoadChatAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set
            {
                if (_isLoadingMore != value)
                {
                    _isLoadingMore = value;
                    OnPropertyChanged();
                }
            }
        }

        private async Task<long> GetUserIdAsync()
        {
            try
            {
                if (_currentUserId > 0)
                    return _currentUserId;

                _currentUserId = await _chatService.GetCurrentUserIdAsync();
                return _currentUserId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user ID");
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
                    if (_messages != null)
                    {
                        _messages.CollectionChanged -= Messages_CollectionChanged;
                    }

                    _messages = value;

                    if (_messages != null)
                    {
                        _messages.CollectionChanged += Messages_CollectionChanged;
                    }

                    OnPropertyChanged();
                    NoMessages = _messages?.Count == 0;
                }
            }
        }

        private void Messages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            NoMessages = Messages.Count == 0;
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
        public ICommand LoadMoreMessagesCommand { get; }

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
            LoadMoreMessagesCommand = new Command(async () => await LoadMoreMessagesAsync());

            // اشتراک در رویدادهای SignalR
            _signalRService.OnMessageReceived += OnMessageReceived;
            _signalRService.OnMessageRead += OnMessageRead;
            _signalRService.OnUserStatusChanged += OnUserStatusChanged;
            _signalRService.OnMessageConfirmed += OnMessageConfirmed;

            // اشتراک در رویداد جدید تأیید همبستگی
            _signalRService.OnMessageCorrelationConfirmed += OnMessageCorrelationConfirmed;

            _logger.LogInformation("ChatViewModel initialized");
        }

        private void OnMessageCorrelationConfirmed(string correlationId, int messageId)
        {
            if (string.IsNullOrEmpty(correlationId) || messageId <= 0)
                return;

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    _logger.LogInformation("Message with correlationId {CorrelationId} confirmed with server ID {MessageId}", correlationId, messageId);

                    // بررسی کنیم آیا یک پیام موقت با این correlationId داریم
                    if (_pendingMessagesByCorrelation.TryRemove(correlationId, out int tempId))
                    {
                        // یافتن پیام موقت در کالکشن
                        var tempIndex = -1;
                        for (int i = 0; i < Messages.Count; i++)
                        {
                            if (Messages[i].Id == tempId)
                            {
                                tempIndex = i;
                                break;
                            }
                        }

                        if (tempIndex >= 0)
                        {
                            // به‌روزرسانی پیام موقت با ID سرور
                            Messages[tempIndex].Id = messageId;
                            Messages[tempIndex].Status = Constants.MessageStatus.Delivered;

                            // پاک کردن از نگاشت ID موقت
                            _pendingMessagesById.TryRemove(tempId, out _);

                            // به‌روزرسانی UI
                            //CollectionExtensions.UpdateItem(Messages, tempIndex);
                            Messages[tempIndex].OnPropertyChanged(nameof(MessageModel.Id));
                            Messages[tempIndex].OnPropertyChanged(nameof(MessageModel.Status));


                            _logger.LogInformation("Updated temp message {TempId} with server ID {MessageId}", tempId, messageId);
                        }
                        else
                        {
                            _logger.LogWarning("Temp message with ID {TempId} not found for correlationId {CorrelationId}", tempId, correlationId);
                        }
                    }
                    else
                    {
                        // شاید پیام قبلاً با ID سرور به‌روزرسانی شده است
                        // تلاش برای به‌روزرسانی وضعیت پیام با ID سرور
                        var messageToUpdate = Messages.FirstOrDefault(m => m.Id == messageId);
                        if (messageToUpdate != null)
                        {
                            messageToUpdate.Status = Constants.MessageStatus.Delivered;
                            messageToUpdate.CorrelationId = correlationId;
                            messageToUpdate.OnPropertyChanged(nameof(MessageModel.Status));
                            _logger.LogInformation("Updated message {MessageId} status to Delivered", messageId);
                        }
                        else
                        {
                            _logger.LogWarning("No message found for correlationId {CorrelationId} or messageId {MessageId}", correlationId, messageId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message correlation confirmation");
                }
            });
        }

        private async Task LoadChatAsync()
        {
            if (string.IsNullOrEmpty(ChatId))
            {
                _logger.LogWarning("ChatId is empty - cannot load chat");
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

                // بارگذاری پیام‌ها
                var messages = await _chatService.GetMessagesAsync(chatGuid);
                _logger.LogInformation("Loaded {Count} messages for chat {ChatId}",
                    messages?.Count ?? 0, chatGuid);

                // اطمینان از بارگذاری ID کاربر
                if (_currentUserId == 0)
                {
                    _currentUserId = await GetUserIdAsync();
                }

                // به‌روزرسانی UI در رشته اصلی
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // تنظیم چت
                        Chat = chat;

                        // اگر پیام‌ها بارگذاری شدند
                        if (messages != null && messages.Count > 0)
                        {
                            // علامت‌گذاری پیام‌ها به عنوان پیام‌های خود یا دیگران
                            foreach (var message in messages)
                            {
                                message.IsOwnMessage = message.SenderId == _currentUserId;

                                // قالب‌بندی زمان اگر هنوز تنظیم نشده
                                if (string.IsNullOrEmpty(message.SentAtFormatted))
                                {
                                    message.SentAtFormatted = FormatTimeDisplay(message.SentAt);
                                }
                            }

                            // مرتب‌سازی بر اساس زمان ارسال
                            var sortedMessages = messages.OrderBy(m => m.SentAt).ToList();

                            // اگر بار اول است، لیست موجود را پاک کنید
                            if (_isFirstLoad)
                            {
                                // ایجاد کالکشن جدید با تمام پیام‌ها
                                var newMessages = new ObservableCollection<MessageModel>(sortedMessages);
                                Messages = newMessages; // فقط یک بار رویداد تغییر را فراخوانی می‌کند
                                _isFirstLoad = false;
                            }
                            else
                            {
                                // اضافه کردن پیام‌های جدید فقط (اگر از قبل وجود ندارند)
                                var existingIds = new HashSet<int>(Messages.Select(m => m.Id));

                                // فقط پیام‌های جدید را اضافه کنیم
                                foreach (var message in sortedMessages)
                                {
                                    if (!existingIds.Contains(message.Id))
                                    {
                                        Messages.Add(message);
                                    }
                                }
                            }
                        }
                        else if (messages != null && messages.Count == 0 && _isFirstLoad)
                        {
                            // اگر پیامی نیست، کالکشن خالی را مستقیماً تنظیم کنیم
                            Messages = new ObservableCollection<MessageModel>();
                            NoMessages = true;
                            _isFirstLoad = false;
                        }

                        // پایان بارگذاری
                        IsLoading = false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating UI in LoadChatAsync");
                    }
                });

                // علامت‌گذاری پیام‌های خوانده نشده به عنوان خوانده شده
                await MarkUnreadMessagesAsReadAsync();

                _messagesSkip = messages?.Count ?? 0;
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
                // یافتن پیام‌های خوانده نشده که توسط کاربر فعلی ارسال نشده‌اند
                var unreadMessageIds = Messages
                    .Where(m => !m.IsOwnMessage && !m.IsRead && m.Id > 0)
                    .Select(m => m.Id)
                    .ToList();

                if (unreadMessageIds.Count > 0)
                {
                    _logger.LogInformation("Marking {Count} messages as read", unreadMessageIds.Count);

                    // علامت‌گذاری به عنوان خوانده شده در سرور
                    await _chatService.MarkAsReadAsync(chatGuid, unreadMessageIds);

                    // به‌روزرسانی وضعیت پیام محلی
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var messageId in unreadMessageIds)
                        {
                            var message = Messages.FirstOrDefault(m => m.Id == messageId);
                            if (message != null)
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
            }
        }

        private async Task SendMessageAsync()
        {
            if (!CanSendMessage || string.IsNullOrEmpty(ChatId) || !Guid.TryParse(ChatId, out var chatGuid))
                return;

            string messageText = MessageText.Trim();
            MessageText = string.Empty;

            try
            {
                IsSending = true;
                _logger.LogInformation("Sending message to chat {ChatId}: {MessageText}", chatGuid, messageText);

                // ایجاد یک شناسه همبستگی منحصربه‌فرد
                string correlationId = Guid.NewGuid().ToString("N");

                // ایجاد یک ID منفی تصادفی برای پیام موقت
                int tempId = -1 * new Random().Next(10000, 99999);

                // ایجاد پیام موقت
                var tempMessage = new MessageModel
                {
                    Id = tempId,
                    Content = messageText,
                    SentAt = DateTime.Now,
                    ChatId = chatGuid,
                    SenderId = await GetUserIdAsync(),
                    SenderName = "You",
                    Status = Constants.MessageStatus.Sending,
                    SentAtFormatted = DateTime.Now.ToString("HH:mm"),
                    IsOwnMessage = true,
                    CorrelationId = correlationId
                };

                // ذخیره پیام موقت در دیکشنری‌ها
                _pendingMessagesById[tempId] = correlationId;
                _pendingMessagesByCorrelation[correlationId] = tempId;

                // فقط پیام جدید را اضافه کنیم - بدون تغییر کل کالکشن
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // پیام موقت را به کالکشن موجود اضافه کنید
                        Messages.Add(tempMessage);
                        NoMessages = Messages.Count == 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error adding temp message to Messages collection");
                    }
                });

                // ارسال پیام به سرور با شناسه همبستگی
                var serverMessage = await _chatService.SendMessageWithCorrelationAsync(chatGuid, messageText, correlationId);

                // به‌روزرسانی پیام موقت با پاسخ سرور - بدون ایجاد کالکشن جدید
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        // یافتن پیام موقت در کالکشن
                        int tempIndex = -1;
                        for (int i = 0; i < Messages.Count; i++)
                        {
                            if (Messages[i].Id == tempId)
                            {
                                tempIndex = i;
                                break;
                            }
                        }

                        if (tempIndex >= 0)
                        {
                            if (serverMessage != null)
                            {
                                // به‌روزرسانی پیام موقت با داده‌های سرور (بدون حذف و اضافه مجدد)
                                Messages[tempIndex].Id = serverMessage.Id;
                                Messages[tempIndex].Status = Constants.MessageStatus.Sent;

                                // اعلان تغییر خصوصیت برای به‌روزرسانی UI
                                Messages[tempIndex].OnPropertyChanged(nameof(MessageModel.Id));
                                Messages[tempIndex].OnPropertyChanged(nameof(MessageModel.Status));

                                // به‌روزرسانی نگاشت‌ها
                                _pendingMessagesById.TryRemove(tempId, out _);
                                _pendingMessagesByCorrelation[correlationId] = serverMessage.Id;

                                _logger.LogInformation("Updated message from temp ID {TempId} to server ID {ServerId}", tempId, serverMessage.Id);
                            }
                            else
                            {
                                // علامت‌گذاری به عنوان ناموفق (بدون حذف و اضافه مجدد)
                                Messages[tempIndex].Status = Constants.MessageStatus.Failed;
                                Messages[tempIndex].OnPropertyChanged(nameof(MessageModel.Status));

                                // پاک کردن از نگاشت‌ها
                                _pendingMessagesById.TryRemove(tempId, out _);
                                _pendingMessagesByCorrelation.TryRemove(correlationId, out _);

                                _logger.LogWarning("Message sending failed, marked as failed");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating temp message with server response");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendMessageAsync: {Message}", ex.Message);
                await _toastService.ShowToastAsync($"Error sending message: {ex.Message}", ToastType.Error);

                // سعی در یافتن و علامت‌گذاری آخرین پیام موقت به عنوان ناموفق
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var lastPendingMessage = Messages
                        .LastOrDefault(m => m.IsOwnMessage && m.Status == Constants.MessageStatus.Sending);

                    if (lastPendingMessage != null)
                    {
                        lastPendingMessage.Status = Constants.MessageStatus.Failed;
                        lastPendingMessage.OnPropertyChanged(nameof(MessageModel.Status));

                        // پاک کردن از نگاشت‌ها اگر امکان‌پذیر است
                        if (lastPendingMessage.Id < 0)
                        {
                            _pendingMessagesById.TryRemove(lastPendingMessage.Id, out string correlationId);

                            if (!string.IsNullOrEmpty(correlationId))
                            {
                                _pendingMessagesByCorrelation.TryRemove(correlationId, out _);
                            }
                        }
                    }
                });
            }
            finally
            {
                IsSending = false;
            }
        }

        private string FormatTimeDisplay(DateTime dateTime)
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
                // بازگشت به قالب ساده در صورت بروز خطا
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

            await _toastService.ShowToastAsync("Profile viewing will be available in a future update", ToastType.Info);
        }

        private void OnMessageReceived(MessageModel message)
        {
            if (Chat == null || message.ChatId.ToString() != ChatId)
                return;

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    _logger.LogInformation("Received message {MessageId} for chat {ChatId}", message.Id, message.ChatId);

                    // تعیین اینکه آیا پیام مال خود کاربر است
                    message.IsOwnMessage = message.SenderId == _currentUserId;

                    // برای پیام‌های خود کاربر، بررسی اینکه آیا این پیام تکراری است یا به‌روزرسانی یک پیام موقت
                    if (message.IsOwnMessage && message.Id > 0)
                    {
                        // بررسی اینکه آیا پیامی با این ID قبلاً دریافت شده است
                        var existingMessage = Messages.FirstOrDefault(m => m.Id == message.Id);
                        if (existingMessage != null)
                        {
                            _logger.LogInformation("Duplicate message with ID {MessageId} ignored", message.Id);
                            return;
                        }

                        // جستجوی یک پیام موقت مطابق از طریق CorrelationId یا محتوا و زمان
                        var matchingTempMessages = Messages
                            .Where(m => m.Id < 0 && m.IsOwnMessage && (
                                (!string.IsNullOrEmpty(message.CorrelationId) && m.CorrelationId == message.CorrelationId) ||
                                (m.Content == message.Content && Math.Abs((m.SentAt - message.SentAt).TotalMinutes) < 2)
                            ))
                            .ToList();

                        if (matchingTempMessages.Any())
                        {
                            // به‌روزرسانی پیام موقت با اطلاعات پیام سرور
                            var tempMessage = matchingTempMessages.First();
                            var tempIndex = Messages.IndexOf(tempMessage);

                            if (tempIndex >= 0)
                            {
                                _logger.LogInformation("Updating temp message {TempId} with server message {MessageId}",
                                    tempMessage.Id, message.Id);

                                // پاک کردن مقدار قدیمی از نگاشت
                                if (_pendingMessagesById.TryRemove(tempMessage.Id, out string correlationId))
                                {
                                    // به‌روزرسانی CorrelationId -> Id نگاشت
                                    _pendingMessagesByCorrelation[correlationId] = message.Id;
                                }

                                // به‌روزرسانی پیام در لیست - بدون جایگزینی عنصر
                                // بجای جایگزینی عنصر، خصوصیت‌های آن را به‌روزرسانی می‌کنیم
                                Messages[tempIndex].Id = message.Id;
                                Messages[tempIndex].SentAt = message.SentAt;
                                Messages[tempIndex].Status = Constants.MessageStatus.Delivered;

                                // اعلان تغییر خصوصیت برای به‌روزرسانی UI
                                Messages[tempIndex].OnPropertyChanged(nameof(MessageModel.Id));
                                Messages[tempIndex].OnPropertyChanged(nameof(MessageModel.SentAt));
                                Messages[tempIndex].OnPropertyChanged(nameof(MessageModel.Status));

                                return;
                            }
                        }
                    }

                    // اگر این یک پیام جدید است (نه تکراری و نه به‌روزرسانی)، آن را اضافه کنیم
                    if (!Messages.Any(m => m.Id == message.Id && m.Id > 0))
                    {
                        _logger.LogInformation("Adding new message {MessageId} to collection", message.Id);

                        // اضافه کردن به کالکشن موجود - بدون ایجاد کالکشن جدید
                        Messages.Add(message);
                        NoMessages = false;

                        // اسکرول به آخرین پیام - باید منطق مربوطه در XAML هم تنظیم شود

                        // اگر این پیام از طرف مقابل است، به عنوان خوانده شده علامت‌گذاری کنیم
                        if (!message.IsOwnMessage)
                        {
                            _ = MarkMessageAsReadAsync(message.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing received message");
                }
            });
        }

        private void OnMessageConfirmed(int messageId)
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    _logger.LogInformation("Message with server ID {MessageId} confirmed", messageId);

                    // یافتن پیام در کالکشن با ID سرور
                    var message = Messages.FirstOrDefault(m => m.Id == messageId && m.IsOwnMessage);
                    if (message != null)
                    {
                        message.Status = Constants.MessageStatus.Delivered;
                        _logger.LogInformation("Updated message {MessageId} status to Delivered", messageId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message confirmation");
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

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    _logger.LogInformation("Message {MessageId} in chat {ChatId} marked as read", messageId, chatId);

                    // یافتن پیام در کالکشن
                    var message = Messages.FirstOrDefault(m => m.Id == messageId && m.IsOwnMessage);
                    if (message != null)
                    {
                        message.IsRead = true;
                        message.ReadAt = DateTime.UtcNow;
                        message.Status = Constants.MessageStatus.Read;
                        _logger.LogInformation("Updated message {MessageId} status to Read", messageId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message read status");
                }
            });
        }

        private async Task LoadMoreMessagesAsync()
        {
            if (IsLoadingMore || string.IsNullOrEmpty(ChatId) || !Guid.TryParse(ChatId, out var chatGuid))
                return;

            try
            {
                // فقط ادامه می‌دهیم اگر پیام داریم و به انتهای لیست نرسیده‌ایم
                if (Messages.Count == 0 || _messagesSkip <= 0)
                    return;

                IsLoadingMore = true;
                _logger.LogInformation("Loading more messages from skip={Skip}", _messagesSkip);

                // بارگذاری پیام‌های بیشتر از سرور
                var messages = await _chatService.GetMessagesAsync(chatGuid, _messagesSkip, MessagesPageSize);

                if (messages != null && messages.Count > 0)
                {
                    _logger.LogInformation("Loaded {Count} more messages", messages.Count);

                    // به‌روزرسانی نقطه skip برای دفعه بعد
                    _messagesSkip += messages.Count;

                    // تنظیم IsOwnMessage برای هر پیام
                    foreach (var message in messages)
                    {
                        message.IsOwnMessage = message.SenderId == _currentUserId;

                        // قالب‌بندی زمان اگر هنوز تنظیم نشده
                        if (string.IsNullOrEmpty(message.SentAtFormatted))
                        {
                            message.SentAtFormatted = FormatTimeDisplay(message.SentAt);
                        }
                    }

                    // افزودن به ابتدای لیست - اینها پیام‌های قدیمی‌تر هستند
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            // بدست آوردن ID پیام‌های موجود
                            var existingIds = Messages.Select(m => m.Id).ToHashSet();

                            // مرتب‌سازی پیام‌ها بر اساس زمان (قدیمی‌ترین در ابتدا)
                            var sortedMessages = messages.OrderBy(m => m.SentAt).ToList();

                            // جدیدترین پیام موجود در مجموعه فعلی
                            var oldestExistingMessage = Messages.FirstOrDefault();
                            var oldestExistingTime = oldestExistingMessage?.SentAt ?? DateTime.MaxValue;

                            // اضافه کردن پیام‌های جدید به ابتدای کالکشن (به ترتیب)
                            int insertedCount = 0;
                            foreach (var message in sortedMessages)
                            {
                                if (!existingIds.Contains(message.Id) && message.SentAt < oldestExistingTime)
                                {
                                    Messages.Insert(insertedCount++, message);
                                }
                            }

                            _logger.LogInformation("Added {Count} older messages to the collection", insertedCount);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error updating UI with older messages");
                        }
                    });
                }
                else
                {
                    _logger.LogInformation("No more messages available");
                    // به انتها رسیده‌ایم - پیام بیشتری برای بارگذاری نیست
                    _messagesSkip = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading more messages");
                await _toastService.ShowToastAsync("Error loading older messages", ToastType.Error);
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private void OnUserStatusChanged(long userId, bool isOnline, DateTime? lastActive)
        {
            if (Chat?.OtherParticipant?.Id == userId)
            {
                MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        _logger.LogInformation("User {UserId} status changed: Online = {IsOnline}, LastActive = {LastActive}",
                            userId, isOnline, lastActive);

                        // به‌روزرسانی وضعیت آنلاین
                        Chat.OtherParticipant.IsOnline = isOnline;
                        Chat.OtherParticipant.LastActive = lastActive;

                        // اجبار به‌روزرسانی UI
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