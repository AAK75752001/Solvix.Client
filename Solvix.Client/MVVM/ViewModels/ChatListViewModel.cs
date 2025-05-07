using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Solvix.Client.MVVM.Views;

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
                    _allChats = chatList.OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt).ToList();

                    foreach (var chat in _allChats)
                    {
                        CalculateOtherParticipant(chat, _currentUserId);
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

            _logger.LogDebug("Message received via SignalR: ChatId={ChatId}, MessageId={MessageId}",
                message.ChatId, message.Id);

            MainThread.BeginInvokeOnMainThread(async () =>
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

                    _logger.LogDebug("Updated chat {ChatId} with new message data", chat.Id);

                    // به‌روزرسانی لیست چت‌ها
                    FilterChats();
                }
                else
                {
                    // چت جدید است، باید لیست چت‌ها را به‌روزرسانی کنیم
                    _logger.LogInformation("Received message for unknown chat {ChatId}. Refreshing chat list...", message.ChatId);
                    await LoadChatsAsync(true);
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
                    (c.OtherParticipant?.PhoneNumber != null && c.OtherParticipant.PhoneNumber.Contains(query))
                );
            }

            // مرتب‌سازی چت‌ها بر اساس زمان آخرین پیام
            var sortedChats = chatsToShow.OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt).ToList();

            // تنظیم لیست جدید
            FilteredChats = new ObservableCollection<ChatModel>(sortedChats);
            OnPropertyChanged(nameof(FilteredChats));

            _logger.LogDebug("Filtered chats count: {Count}", FilteredChats.Count);
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
            _logger.LogInformation("New Chat command executed.");
            await _toastService.ShowToastAsync("شروع چت جدید (به زودی!)", ToastType.Info);
        }

        [RelayCommand]
        private async Task GoToSettingsAsync()
        {
            _logger.LogInformation("Go To Settings command executed.");
            await _toastService.ShowToastAsync("رفتن به تنظیمات (به زودی!)", ToastType.Info);
        }

        public async Task OnAppearingAsync()
        {
            _logger.LogInformation("ChatListPage appearing. Loading chats...");

            // تلاش برای اتصال به SignalR
            if (!_signalRService.IsConnected)
            {
                await InitializeSignalRAsync();
            }

            await LoadChatsAsync();
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