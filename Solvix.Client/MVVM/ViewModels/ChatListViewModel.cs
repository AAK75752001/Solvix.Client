using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Solvix.Client.MVVM.Views;
using System.Globalization;

namespace Solvix.Client.MVVM.ViewModels
{
    public partial class ChatListViewModel : ObservableObject, IDisposable
    {
        private readonly IChatService _chatService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        private readonly ISignalRService _signalRService;
        private readonly ILogger<ChatListViewModel> _logger;
        private bool _isDisposed = false;
        private long _currentUserId;

        private List<ChatModel> _allChats = new();

        [ObservableProperty]
        private ObservableCollection<ChatModel> _filteredChats = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isRefreshing;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ChatModel? _selectedChat;

        [ObservableProperty]
        private bool _isConnected;

        public ChatListViewModel(
            IChatService chatService,
            IToastService toastService,
            IAuthService authService,
            ISignalRService signalRService,
            ILogger<ChatListViewModel> logger)
        {
            _chatService = chatService;
            _toastService = toastService;
            _authService = authService;
            _signalRService = signalRService;
            _logger = logger;

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SearchQuery))
                {
                    FilterChats();
                }
            };

            // اشتراک در رویدادهای SignalR
            _signalRService.OnUserStatusChanged += SignalRUserStatusChanged;
            _signalRService.OnConnectionStateChanged += SignalRConnectionStateChanged;
            _signalRService.OnMessageReceived += SignalRMessageReceived;

            IsConnected = _signalRService.IsConnected;
        }

        // دستور برای بارگذاری چت‌ها
        [RelayCommand]
        private async Task LoadChatsAsync(bool forceRefresh = false)
        {
            if (IsLoading && !forceRefresh) return;

            IsLoading = true;
            _logger.LogInformation("Loading chats... Force refresh: {ForceRefresh}", forceRefresh);

            try
            {
                // اگر SignalR متصل نیست، تلاش کنیم متصل شویم
                if (!_signalRService.IsConnected)
                {
                    await InitializeSignalRAsync();
                }

                // دریافت آیدی کاربر جاری
                _currentUserId = await _authService.GetUserIdAsync();
                if (_currentUserId == 0)
                {
                    _logger.LogError("Failed to get current user ID");
                    await _toastService.ShowToastAsync("خطا در احراز هویت کاربر", ToastType.Error);
                    return;
                }

                var chatList = await _chatService.GetUserChatsAsync();

                if (chatList != null)
                {
                    // ذخیره چت‌های فعلی برای مقایسه
                    var existingChats = _allChats.ToDictionary(c => c.Id);

                    // ادغام اطلاعات جدید با موجود
                    foreach (var newChat in chatList)
                    {
                        if (existingChats.TryGetValue(newChat.Id, out var existingChat))
                        {
                            // اگر چت وجود داشته و آخرین پیام آن جدیدتر بوده، از آن استفاده کنیم
                            if (existingChat.LastMessageTime > newChat.LastMessageTime)
                            {
                                newChat.LastMessage = existingChat.LastMessage;
                                newChat.LastMessageTime = existingChat.LastMessageTime;
                                _logger.LogDebug("Using more recent LastMessage from existing chat {ChatId}", newChat.Id);
                            }

                            // حفظ UnreadCount
                            newChat.UnreadCount = existingChat.UnreadCount;
                        }
                    }

                    // مرتب‌سازی چت‌ها بر اساس زمان آخرین پیام
                    _allChats = chatList.OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt).ToList();

                    foreach (var chat in _allChats)
                    {
                        CalculateOtherParticipant(chat, _currentUserId);

                        // اطمینان از داشتن مقادیر معتبر برای LastMessage و LastMessageTime
                        EnsureChatProperties(chat);
                    }

                    FilterChats();
                    _logger.LogInformation("Chats loaded and filtered successfully. Count: {Count}", _allChats.Count);
                }
                else
                {
                    _logger.LogWarning("GetUserChatsAsync returned null.");
                    await _toastService.ShowToastAsync("خطا در بارگذاری لیست چت‌ها", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chats.");
                await _toastService.ShowToastAsync($"خطا: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsLoading = false;
                IsRefreshing = false;
            }
        }

        // اطمینان از داشتن مقادیر معتبر برای LastMessage و LastMessageTime
        private void EnsureChatProperties(ChatModel chat)
        {
            // اگر LastMessage و LastMessageTime وجود نداشته باشند، مقادیر پیش‌فرض تنظیم می‌کنیم
            if (string.IsNullOrEmpty(chat.LastMessage))
            {
                if (chat.Messages != null && chat.Messages.Any())
                {
                    var lastMsg = chat.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault();
                    if (lastMsg != null)
                    {
                        chat.LastMessage = lastMsg.Content;
                        chat.LastMessageTime = lastMsg.SentAt;
                        _logger.LogDebug("Fixed missing LastMessage for chat {ChatId} from Messages collection", chat.Id);
                    }
                }

                // اگر هنوز هم آخرین پیام خالی است، آن را از پیش‌فرض استفاده کنیم
                // ولی نباید "آغاز گفتگو" را نمایش دهیم مگر اینکه واقعاً هیچ پیامی وجود نداشته باشد
                if (string.IsNullOrEmpty(chat.LastMessage))
                {
                    // فقط در صورتی که چت جدید است و پیامی ندارد، "آغاز گفتگو" را نمایش می‌دهیم
                    // در غیر این صورت، ممکن است پیام‌ها هنوز دریافت نشده باشند
                    chat.LastMessage = "...";
                    _logger.LogDebug("Set placeholder LastMessage for chat {ChatId}", chat.Id);

                    // برای زمان پیام هم اگر وجود ندارد، از زمان ایجاد چت استفاده می‌کنیم
                    if (!chat.LastMessageTime.HasValue)
                    {
                        chat.LastMessageTime = chat.CreatedAt;
                        _logger.LogDebug("Set default LastMessageTime to chat creation time for chat {ChatId}", chat.Id);
                    }
                }
            }

            // اطمینان از قالب‌بندی صحیح LastMessageTimeFormatted
            if (chat.LastMessageTime.HasValue)
            {
                var localDateTime = chat.LastMessageTime.Value.Kind == DateTimeKind.Utc
                    ? chat.LastMessageTime.Value.ToLocalTime()
                    : chat.LastMessageTime.Value;

                var today = DateTime.Now.Date;

                string formattedTime;
                if (localDateTime.Date == today)
                {
                    formattedTime = localDateTime.ToString("HH:mm");
                }
                else if (today.Subtract(localDateTime.Date).TotalDays < 7)
                {
                    formattedTime = localDateTime.ToString("ddd", new CultureInfo("fa-IR")); // استفاده از فرهنگ فارسی
                }
                else
                {
                    formattedTime = localDateTime.ToString("yyyy/MM/dd");
                }

                _logger.LogDebug("Updated LastMessageTimeFormatted for chat {ChatId} to {FormattedTime}", chat.Id, formattedTime);
            }

            // اطمینان از داشتن DisplayTitle
            if (chat.OtherParticipant != null)
            {
                _logger.LogDebug("OtherParticipant set for chat {ChatId}: {Name}", chat.Id, chat.OtherParticipant.DisplayName);
            }
        }

        // اتصال به SignalR و پیکربندی رویدادها
        private async Task InitializeSignalRAsync()
        {
            try
            {
                _logger.LogInformation("Initializing SignalR connection...");
                await _signalRService.StartAsync();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing SignalR");
            }
        }

        // تنظیم طرف دیگر مکالمه در چت
        private void CalculateOtherParticipant(ChatModel chat, long currentUserId)
        {
            if (!chat.IsGroup && chat.Participants != null && chat.Participants.Any())
            {
                chat.OtherParticipant = chat.Participants.FirstOrDefault(p => p.Id != currentUserId);

                // اطمینان از وجود اطلاعات حداقلی برای طرف مقابل
                if (chat.OtherParticipant != null)
                {
                    _logger.LogDebug("Other participant for chat {ChatId}: {UserId} - {Name} - Online: {IsOnline}",
                        chat.Id, chat.OtherParticipant.Id, chat.OtherParticipant.DisplayName, chat.OtherParticipant.IsOnline);
                }
                else
                {
                    _logger.LogWarning("No other participant found for chat {ChatId}", chat.Id);
                }
            }
        }

        // رویدادهای SignalR
        private void SignalRUserStatusChanged(long userId, bool isOnline)
        {
            if (_isDisposed) return;

            _logger.LogInformation("User status changed: UserId={UserId}, IsOnline={IsOnline}", userId, isOnline);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var updatedChats = false;

                // بررسی و به‌روزرسانی وضعیت آنلاین کاربر در تمام چت‌ها
                foreach (var chat in _allChats.Where(c => !c.IsGroup && c.OtherParticipant?.Id == userId))
                {
                    chat.OtherParticipant.IsOnline = isOnline;

                    // اگر کاربر آفلاین شد، زمان آخرین فعالیت را به‌روز کنیم
                    if (!isOnline && chat.OtherParticipant != null)
                    {
                        chat.OtherParticipant.LastActive = DateTime.UtcNow;
                    }

                    updatedChats = true;
                    _logger.LogDebug("Updated online status for user {UserId} in chat {ChatId} to {IsOnline}",
                        userId, chat.Id, isOnline);
                }

                // اگر تغییری در چت‌ها داشتیم، لیست را به‌روز کنیم
                if (updatedChats)
                {
                    FilterChats();
                }
            });
        }

        private void SignalRConnectionStateChanged(bool isConnected)
        {
            if (_isDisposed) return;

            _logger.LogInformation("SignalR connection state changed: {IsConnected}", isConnected);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsConnected = isConnected;

                // اگر دوباره متصل شدیم، لیست چت‌ها را تازه‌سازی کنیم
                if (isConnected)
                {
                    LoadChatsAsync(true).ConfigureAwait(false);
                }
            });
        }

        private void SignalRMessageReceived(MessageModel message)
        {
            if (_isDisposed) return;

            _logger.LogDebug("Message received via SignalR: ChatId={ChatId}, MessageId={MessageId}, Content={Content}",
                message.ChatId, message.Id, message.Content?.Substring(0, Math.Min(20, message.Content?.Length ?? 0)));

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // پیدا کردن چت مربوط به پیام
                    var chat = _allChats.FirstOrDefault(c => c.Id == message.ChatId);

                    if (chat != null)
                    {
                        // به‌روزرسانی آخرین پیام
                        chat.LastMessage = message.Content;
                        chat.LastMessageTime = message.SentAt;

                        // اگر پیام از طرف مقابل است و چت انتخاب نشده، تعداد پیام‌های نخوانده را افزایش دهیم
                        if (message.SenderId != _currentUserId && (_selectedChat == null || _selectedChat.Id != chat.Id))
                        {
                            chat.UnreadCount++;
                        }

                        _logger.LogDebug("Updated chat {ChatId} with new message data. LastMessage: {LastMessage}, LastMessageTime: {LastMessageTime}",
                            chat.Id, chat.LastMessage?.Substring(0, Math.Min(20, chat.LastMessage?.Length ?? 0)), chat.LastMessageTime);

                        // اطمینان از به‌روزرسانی زمان آخرین پیام
                        if (chat.LastMessageTime == null || chat.LastMessageTime == default)
                        {
                            chat.LastMessageTime = DateTime.UtcNow;
                        }

                        // مرتب‌سازی مجدد چت‌ها بر اساس زمان آخرین پیام
                        _allChats = _allChats.OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt).ToList();

                        // به‌روزرسانی لیست چت‌ها
                        FilterChats();
                    }
                    else
                    {
                        // چت جدید است، باید لیست چت‌ها را به‌روزرسانی کنیم
                        _logger.LogInformation("Received message for unknown chat {ChatId}. Refreshing chat list...", message.ChatId);
                        await LoadChatsAsync(true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing received message: {ErrorMessage}", ex.Message);
                }
            });
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            IsRefreshing = true;
            await LoadChatsAsync(forceRefresh: true);
        }

        private void FilterChats()
        {
            var query = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

            IEnumerable<ChatModel> chatsToShow;

            if (string.IsNullOrWhiteSpace(query))
            {
                chatsToShow = _allChats;
            }
            else
            {
                chatsToShow = _allChats.Where(c =>
                    (c.DisplayTitle != null && c.DisplayTitle.ToLowerInvariant().Contains(query)) ||
                    (c.LastMessage != null && c.LastMessage.ToLowerInvariant().Contains(query)) ||
                    (c.OtherParticipant?.PhoneNumber != null && c.OtherParticipant.PhoneNumber.Contains(query)) ||
                    (c.OtherParticipant?.DisplayName != null && c.OtherParticipant.DisplayName.ToLowerInvariant().Contains(query))
                );
            }

            // مرتب‌سازی چت‌ها بر اساس زمان آخرین پیام
            var sortedChats = chatsToShow.OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt).ToList();

            // تنظیم لیست جدید
            FilteredChats = new ObservableCollection<ChatModel>(sortedChats);
            OnPropertyChanged(nameof(FilteredChats));

            _logger.LogDebug("Filtered chats count: {Count}, with most recent chat at: {RecentTime}",
                FilteredChats.Count,
                FilteredChats.Count > 0 ? FilteredChats[0].LastMessageTime?.ToString() : "N/A");
        }

        [RelayCommand]
        private async Task GoToChatAsync(ChatModel? chat)
        {
            if (chat == null)
            {
                _logger.LogWarning("GoToChatAsync called with null chat.");
                return;
            }

            try
            {
                // تنظیم چت انتخاب شده
                SelectedChat = chat;

                _logger.LogInformation("Navigating to ChatPage for ChatId: {ChatId}", chat.Id);
                await Shell.Current.GoToAsync($"{nameof(ChatPage)}?ChatId={chat.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation failed for ChatId: {ChatId}", chat.Id);
                await _toastService.ShowToastAsync("خطا در باز کردن صفحه چت.", ToastType.Error);
            }
            finally
            {
                SelectedChat = null;
            }
        }

        [RelayCommand]
        private async Task SearchTriggeredAsync()
        {
            _logger.LogInformation("Search triggered with query: {Query}", SearchQuery);
            FilterChats();
        }

        [RelayCommand]
        private async Task NewChatAsync()
        {
            _logger.LogInformation("درخواست شروع چت جدید");
            try
            {
                await Shell.Current.GoToAsync(nameof(NewChatPage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ناوبری به صفحه چت جدید");
                await _toastService.ShowToastAsync("خطا در باز کردن صفحه چت جدید", ToastType.Error);
            }
        }

        [RelayCommand]
        private async Task GoToSettingsAsync()
        {
            _logger.LogInformation("درخواست رفتن به صفحه تنظیمات");
            try
            {
                await Shell.Current.GoToAsync(nameof(SettingsPage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ناوبری به صفحه تنظیمات");
                await _toastService.ShowToastAsync("خطا در بازکردن صفحه تنظیمات", ToastType.Error);
            }
        }

        public async Task OnAppearingAsync()
        {
            _logger.LogInformation("ChatListPage appearing. Loading chats...");

            // تلاش برای اتصال به SignalR
            if (!_signalRService.IsConnected)
            {
                await InitializeSignalRAsync();
            }

            await LoadChatsAsync(true); // همیشه یک به‌روزرسانی کامل انجام شود
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
                _logger.LogInformation("Disposing ChatListViewModel");

                // لغو اشتراک در رویدادهای SignalR
                _signalRService.OnUserStatusChanged -= SignalRUserStatusChanged;
                _signalRService.OnConnectionStateChanged -= SignalRConnectionStateChanged;
                _signalRService.OnMessageReceived -= SignalRMessageReceived;
            }

            _isDisposed = true;
        }
    }
}