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
        private bool _isLoadingMore;
        private int _messagesSkip = 0;
        private const int MessagesPageSize = 30;

        // محافظت از تغییرات همزمان در کالکشن پیام‌ها
        private readonly object _messagesLock = new object();

        // آرایه برای ردیابی پیام‌های ارسال شده موقت برای جلوگیری از تکرار
        private readonly Dictionary<string, MessageModel> _tempMessagesByContent = new();

        public string ChatId
        {
            get => _chatId;
            set
            {
                if (_chatId != value)
                {
                    _chatId = value;
                    OnPropertyChanged();

                    // بارگذاری چت زمانی که ChatId تنظیم می‌شود
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
                // اگر از قبل داریم، برگردانیم
                if (_currentUserId > 0)
                    return _currentUserId;

                // اولین بار باید از سرویس دریافت کنیم
                _currentUserId = await _chatService.GetCurrentUserIdAsync();
                return _currentUserId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت شناسه کاربر فعلی");
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
                lock (_messagesLock)
                {
                    if (_messages != value)
                    {
                        _messages = value;
                        OnPropertyChanged();
                        NoMessages = _messages.Count == 0;
                    }
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

            _logger.LogInformation("ChatViewModel initialized");
        }

        // مدیریت پیام‌ها - اضافه کردن یا به‌روزرسانی بدون پاک کردن کالکشن
        private async Task AddOrUpdateMessagesAsync(List<MessageModel> newMessages, bool addToBeginning = false)
        {
            if (newMessages == null || newMessages.Count == 0)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                lock (_messagesLock)
                {
                    var currentUserId = _currentUserId;
                    var messagesAdded = 0;

                    // اصلاح پیام‌های جدید قبل از اضافه کردن
                    foreach (var message in newMessages)
                    {
                        // تنظیم خصوصیت IsOwnMessage
                        message.IsOwnMessage = message.SenderId == currentUserId;

                        // تنظیم زمان نمایشی برای هر پیام
                        if (string.IsNullOrEmpty(message.SentAtFormatted))
                        {
                            message.SentAtFormatted = FormatTimeDisplay(message.SentAt);
                        }

                        // بررسی تکراری نبودن پیام
                        var existingMessage = _messages.FirstOrDefault(m =>
                            (m.Id > 0 && m.Id == message.Id) || // تطابق با شناسه برای پیام‌های سرور
                            (m.Id < 0 && m.Content == message.Content && // تطابق با محتوا و زمان برای پیام‌های موقت
                            Math.Abs((m.SentAt - message.SentAt).TotalSeconds) < 60));

                        if (existingMessage != null)
                        {
                            // به‌روزرسانی پیام موجود
                            existingMessage.Id = message.Id > 0 ? message.Id : existingMessage.Id;
                            existingMessage.Status = message.Status;
                            existingMessage.IsRead = message.IsRead;
                            existingMessage.ReadAt = message.ReadAt;

                            // اگر پیام موقت بود و الان آیدی سرور داره، آیدی رو به‌روزرسانی کنیم
                            if (existingMessage.Id < 0 && message.Id > 0)
                            {
                                existingMessage.Id = message.Id;
                                _logger.LogDebug("آیدی پیام موقت به‌روزرسانی شد: {TempId} -> {MessageId}",
                                    existingMessage.Id, message.Id);
                            }
                        }
                        else
                        {
                            // پیام جدید - افزودن به کالکشن
                            if (addToBeginning)
                            {
                                _messages.Insert(0, message);
                            }
                            else
                            {
                                _messages.Add(message);
                            }
                            messagesAdded++;
                        }
                    }

                    if (messagesAdded > 0)
                    {
                        // مرتب‌سازی پیام‌ها بر اساس زمان
                        var sortedMessages = new ObservableCollection<MessageModel>(
                            _messages.OrderBy(m => m.SentAt).ToList());

                        _messages = sortedMessages;
                        NoMessages = false;
                        OnPropertyChanged(nameof(Messages));
                    }
                }
            });
        }

        private async Task LoadChatAsync()
        {
            if (string.IsNullOrEmpty(ChatId))
            {
                _logger.LogWarning("ChatId خالی است - نمی‌توان چت را بارگذاری کرد");
                return;
            }

            if (!Guid.TryParse(ChatId, out var chatGuid))
            {
                _logger.LogError("قالب ChatId نامعتبر است: {ChatId}", ChatId);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await _toastService.ShowToastAsync("شناسه چت نامعتبر است", ToastType.Error));
                return;
            }

            try
            {
                // فقط در صورت نبود پیام نمایش لودینگ
                if (Messages.Count == 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => IsLoading = true);
                }

                // بارگذاری اطلاعات چت
                var chat = await _chatService.GetChatAsync(chatGuid);
                if (chat == null)
                {
                    _logger.LogWarning("چت پیدا نشد: {ChatId}", chatGuid);
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        IsLoading = false;
                        await _toastService.ShowToastAsync("چت پیدا نشد", ToastType.Error);
                    });
                    return;
                }

                // بارگذاری پیام‌ها
                var messages = await _chatService.GetMessagesAsync(chatGuid);
                _logger.LogInformation("{Count} پیام برای چت {ChatId} بارگذاری شد",
                    messages?.Count ?? 0, chatGuid);

                // اطمینان از بارگذاری شناسه کاربر
                if (_currentUserId == 0)
                {
                    _currentUserId = await GetUserIdAsync();
                }

                // به‌روزرسانی رابط کاربری در رشته اصلی
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // تنظیم چت
                    Chat = chat;

                    // به‌روزرسانی پیام‌ها اگر بارگذاری شده باشند
                    if (messages != null && messages.Count > 0)
                    {
                        AddOrUpdateMessagesAsync(messages).ConfigureAwait(false);
                    }
                    else if (messages != null && messages.Count == 0 && Messages.Count == 0)
                    {
                        NoMessages = true;
                    }

                    // پایان بارگذاری
                    IsLoading = false;
                });

                // علامت‌گذاری پیام‌های خوانده‌نشده به عنوان خوانده‌شده
                await MarkUnreadMessagesAsReadAsync();

                _messagesSkip = messages?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری چت {ChatId}", chatGuid);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsLoading = false;
                    _toastService.ShowToastAsync($"خطا در بارگذاری چت: {ex.Message}", ToastType.Error);
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
                // پیدا کردن پیام‌های خوانده‌نشده که توسط کاربر فعلی ارسال نشده‌اند
                var unreadMessageIds = Messages
                    .Where(m => !m.IsOwnMessage && !m.IsRead && m.Id > 0)
                    .Select(m => m.Id)
                    .ToList();

                if (unreadMessageIds.Count > 0)
                {
                    _logger.LogInformation("علامت‌گذاری {Count} پیام به عنوان خوانده‌شده", unreadMessageIds.Count);

                    // علامت‌گذاری به عنوان خوانده‌شده در سرور
                    await _chatService.MarkAsReadAsync(chatGuid, unreadMessageIds);

                    // به‌روزرسانی وضعیت پیام محلی
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        lock (_messagesLock)
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
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در علامت‌گذاری پیام‌ها به عنوان خوانده‌شده");
            }
        }

        private async Task SendMessageAsync()
        {
            if (!CanSendMessage || string.IsNullOrEmpty(ChatId) || !Guid.TryParse(ChatId, out var chatGuid))
                return;

            string messageText = MessageText.Trim();
            string messageKey = $"{chatGuid}:{messageText.GetHashCode()}:{DateTime.Now.Ticks}";

            // پاک کردن فیلد پیام برای تجربه کاربری بهتر
            MessageText = string.Empty;

            try
            {
                IsSending = true;

                _logger.LogInformation("ارسال پیام به چت {ChatId}: {MessageText}", chatGuid, messageText);

                // دریافت شناسه کاربر فعلی
                if (_currentUserId == 0)
                {
                    _currentUserId = await GetUserIdAsync();
                }

                // ایجاد پیام موقت با یک شناسه منفی منحصر به فرد
                var tempId = -DateTime.Now.Millisecond - 1000 * new Random().Next(1000, 9999);
                var tempMessage = new MessageModel
                {
                    Id = tempId,
                    Content = messageText,
                    SentAt = DateTime.Now, // زمان محلی برای نمایش فوری
                    ChatId = chatGuid,
                    SenderId = _currentUserId,
                    SenderName = "شما", // بعداً با پاسخ سرور جایگزین می‌شود
                    Status = Constants.MessageStatus.Sending, // در ابتدا به عنوان در حال ارسال نشان داده می‌شود
                    SentAtFormatted = FormatTimeDisplay(DateTime.Now),
                    IsOwnMessage = true // به صراحت برای رابط کاربری تنظیم می‌شود
                };

                // افزودن فوری به کالکشن برای بازخورد آنی
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    lock (_messagesLock)
                    {
                        _messages.Add(tempMessage);
                        _tempMessagesByContent[messageKey] = tempMessage;
                        NoMessages = false;
                        OnPropertyChanged(nameof(Messages));
                    }
                });

                // ارسال پیام از طریق API
                MessageModel serverMessage = null;
                try
                {
                    serverMessage = await _chatService.SendMessageAsync(chatGuid, messageText);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در ارسال پیام به سرور");
                }

                // پردازش پاسخ سرور
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    lock (_messagesLock)
                    {
                        // پیدا کردن پیام موقت
                        var tempMsg = _messages.FirstOrDefault(m => m.Id == tempId);

                        if (tempMsg != null)
                        {
                            if (serverMessage != null)
                            {
                                // به‌روزرسانی پیام موقت با داده‌های سرور
                                tempMsg.Id = serverMessage.Id;
                                tempMsg.Status = Constants.MessageStatus.Sent; // تغییر به تیک تکی
                                tempMsg.SentAt = serverMessage.SentAt.ToLocalTime(); // تبدیل به زمان محلی
                                tempMsg.SentAtFormatted = FormatTimeDisplay(serverMessage.SentAt.ToLocalTime());
                                tempMsg.SenderName = serverMessage.SenderName;

                                _logger.LogInformation("پیام موقت {TempId} با شناسه سرور {MessageId} به‌روزرسانی شد",
                                    tempId, serverMessage.Id);

                                // حذف از پیام‌های موقت
                                _tempMessagesByContent.Remove(messageKey);
                            }
                            else
                            {
                                // علامت‌گذاری پیام موقت به عنوان ارسال‌نشده
                                tempMsg.Status = Constants.MessageStatus.Failed;
                                _logger.LogWarning("پیام {TempId} به عنوان ارسال‌نشده علامت‌گذاری شد", tempId);
                            }
                        }
                    }
                });

                if (serverMessage == null)
                {
                    _logger.LogWarning("ارسال پیام ناموفق بود");
                    await _toastService.ShowToastAsync("ارسال پیام ناموفق بود", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در SendMessageAsync: {Message}", ex.Message);
                await _toastService.ShowToastAsync($"خطا در ارسال پیام: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsSending = false;
            }
        }

        // تابع کمکی برای قالب‌بندی زمان نمایشی
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
                // بازگشت به قالب ساده در صورت خطا
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
                _logger.LogError(ex, "خطا در بازگشت به صفحه قبل");
            }
        }

        private async Task ViewProfileAsync()
        {
            if (Chat?.OtherParticipant == null)
                return;

            await _toastService.ShowToastAsync("مشاهده پروفایل کاربر در نسخه‌های آینده در دسترس خواهد بود", ToastType.Info);
        }

        private void OnMessageReceived(MessageModel message)
        {
            if (Chat == null || message.ChatId.ToString() != ChatId)
                return;

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    _logger.LogInformation("پیام {MessageId} برای چت {ChatId} از userId {SenderId} دریافت شد",
                        message.Id, message.ChatId, message.SenderId);

                    lock (_messagesLock)
                    {
                        // تنظیم IsOwnMessage بر اساس شناسه فرستنده
                        bool isOwnMessage = message.SenderId == _currentUserId;
                        message.IsOwnMessage = isOwnMessage;

                        // بررسی برای تکراری نبودن پیام
                        var existingMessage = _messages.FirstOrDefault(m =>
                            (m.Id > 0 && m.Id == message.Id) || // تطابق با شناسه
                            (m.Content == message.Content &&
                             Math.Abs((m.SentAt - message.SentAt).TotalSeconds) < 60)); // یا با محتوا و زمان

                        if (existingMessage != null)
                        {
                            // به‌روزرسانی وضعیت، خوانده‌شده، غیره
                            existingMessage.Status = message.Status;
                            existingMessage.IsRead = message.IsRead;
                            existingMessage.ReadAt = message.ReadAt;

                            // اگر این یک پیام موقت بود که اکنون یک شناسه سرور دارد، آن را به‌روزرسانی کنیم
                            if (existingMessage.Id < 0 && message.Id > 0)
                            {
                                existingMessage.Id = message.Id;
                            }
                        }
                        else
                        {
                            // پیام جدید، آن را به کالکشن اضافه کنیم
                            _messages.Add(message);
                            NoMessages = false;

                            // اگر پیام دریافتی است، آن را به عنوان خوانده‌شده علامت‌گذاری کنیم
                            if (!isOwnMessage)
                            {
                                message.IsRead = true;
                                message.ReadAt = DateTime.UtcNow;
                                MarkMessageAsReadAsync(message.Id).ConfigureAwait(false);
                            }

                            // مرتب‌سازی پیام‌ها بر اساس زمان
                            var sortedMessages = new ObservableCollection<MessageModel>(
                                _messages.OrderBy(m => m.SentAt).ToList());

                            _messages = sortedMessages;
                            OnPropertyChanged(nameof(Messages));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در پردازش پیام دریافتی");
                }
            });
        }

        private void OnMessageConfirmed(int messageId)
        {
            _logger.LogInformation("پیام {MessageId} توسط سرور تأیید شد", messageId);

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                lock (_messagesLock)
                {
                    // پیدا کردن پیام با این شناسه در کالکشن
                    var message = _messages.FirstOrDefault(m => m.Id == messageId);

                    if (message != null)
                    {
                        // تغییر وضعیت به ارسال‌شده (تیک تکی)
                        message.Status = Constants.MessageStatus.Sent;
                    }
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
                _logger.LogError(ex, "خطا در علامت‌گذاری پیام {MessageId} به عنوان خوانده‌شده", messageId);
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
                    _logger.LogInformation("پیام {MessageId} در چت {ChatId} به عنوان خوانده‌شده علامت‌گذاری شد",
                        messageId, chatId);

                    lock (_messagesLock)
                    {
                        // پیدا کردن پیام در کالکشن
                        var message = _messages.FirstOrDefault(m => m.Id == messageId);

                        if (message != null && message.IsOwnMessage)
                        {
                            _logger.LogInformation("به‌روزرسانی وضعیت پیام {MessageId} به READ", messageId);

                            // به‌روزرسانی وضعیت به "خوانده‌شده" (دو تیک)
                            message.IsRead = true;
                            message.ReadAt = DateTime.UtcNow;
                            message.Status = Constants.MessageStatus.Read;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در پردازش به‌روزرسانی وضعیت خواندن پیام");
                }
            });
        }

        private async Task LoadMoreMessagesAsync()
        {
            if (IsLoadingMore || string.IsNullOrEmpty(ChatId) || !Guid.TryParse(ChatId, out var chatGuid))
                return;

            try
            {
                // فقط ادامه بده اگر پیام داریم و به انتهای لیست نرسیده‌ایم
                if (Messages.Count == 0 || _messagesSkip <= 0)
                    return;

                IsLoadingMore = true;
                _logger.LogInformation("بارگذاری پیام‌های بیشتر از skip={Skip}", _messagesSkip);

                // بارگذاری پیام‌های بیشتر از سرور
                var messages = await _chatService.GetMessagesAsync(chatGuid, _messagesSkip, MessagesPageSize);

                if (messages != null && messages.Count > 0)
                {
                    _logger.LogInformation("{Count} پیام بیشتر بارگذاری شد", messages.Count);

                    // به‌روزرسانی نقطه شروع برای دفعه بعد
                    _messagesSkip += messages.Count;

                    // تنظیم IsOwnMessage برای هر پیام
                    foreach (var message in messages)
                    {
                        message.IsOwnMessage = message.SenderId == _currentUserId;
                    }

                    await AddOrUpdateMessagesAsync(messages, true);
                }
                else
                {
                    _logger.LogInformation("پیام بیشتری وجود ندارد");
                    // به انتها رسیدیم - پیام بیشتری برای بارگذاری وجود ندارد
                    _messagesSkip = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری پیام‌های بیشتر");
                await _toastService.ShowToastAsync("خطا در بارگذاری پیام‌های قدیمی‌تر", ToastType.Error);
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
                        _logger.LogInformation("وضعیت کاربر {UserId} تغییر کرد: آنلاین = {IsOnline}, آخرین فعالیت = {LastActive}",
                            userId, isOnline, lastActive);

                        // وضعیت آنلاین را فقط اگر کاربر واقعاً آنلاین است نمایش بده
                        Chat.OtherParticipant.IsOnline = isOnline;
                        Chat.OtherParticipant.LastActive = lastActive;

                        // به‌روزرسانی اجباری رابط کاربری
                        OnPropertyChanged(nameof(Chat));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطا در به‌روزرسانی وضعیت کاربر");
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