using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using Solvix.Client.MVVM.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Solvix.Client.MVVM.ViewModels
{
    public class ChatListViewModel : INotifyPropertyChanged
    {
        private readonly IChatService _chatService;
        private readonly ISignalRService _signalRService;
        private readonly IToastService _toastService;
        private readonly IUserService _userService;
        private readonly ILogger<ChatListViewModel> _logger;

        private bool _isLoading;
        private bool _isRefreshing;
        private string _searchQuery = string.Empty;
        private ObservableCollection<ChatModel> _chats = new();
        private ObservableCollection<ChatModel> _filteredChats = new();
        private readonly Dictionary<long, bool> _userOnlineStatus = new();
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);

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

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                if (_isRefreshing != value)
                {
                    _isRefreshing = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    OnPropertyChanged();
                    FilterChats();
                }
            }
        }

        public ObservableCollection<ChatModel> Chats
        {
            get => _chats;
            set
            {
                if (_chats != value)
                {
                    _chats = value;
                    OnPropertyChanged();
                    FilterChats();
                }
            }
        }

        public ObservableCollection<ChatModel> FilteredChats
        {
            get => _filteredChats;
            set
            {
                if (_filteredChats != value)
                {
                    _filteredChats = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasChats));
                    OnPropertyChanged(nameof(IsEmpty));
                }
            }
        }

        public bool HasChats => FilteredChats.Count > 0;

        public bool IsEmpty => !IsLoading && !HasChats;

        public ICommand RefreshCommand { get; }
        public ICommand ChatSelectedCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public ChatListViewModel(
            IChatService chatService,
            ISignalRService signalRService,
            IToastService toastService,
            IUserService userService,
            ILogger<ChatListViewModel> logger)
        {
            _chatService = chatService;
            _signalRService = signalRService;
            _toastService = toastService;
            _userService = userService;
            _logger = logger;

            RefreshCommand = new Command(async () => await LoadChatsAsync(true));
            ChatSelectedCommand = new Command<ChatModel>(async (chat) => await ChatSelectedAsync(chat));
            SearchCommand = new Command(() => FilterChats());
            ClearSearchCommand = new Command(() => SearchQuery = string.Empty);

            // Subscribe to SignalR events
            _signalRService.OnMessageReceived += OnMessageReceived;
            _signalRService.OnMessageRead += OnMessageRead;
            _signalRService.OnUserStatusChanged += OnUserStatusChanged;

            // Initialize with some empty data to prevent UI issues
            FilteredChats = new ObservableCollection<ChatModel>();

            // Initial load
            LoadChatsAsync().ConfigureAwait(false);

            // Also load online users
            LoadOnlineUsersAsync().ConfigureAwait(false);
        }

        public async Task LoadChatsAsync(bool isRefresh = false)
        {
            // Use a lock to prevent multiple concurrent loads
            if (!await _loadLock.WaitAsync(0))
            {
                _logger.LogInformation("Chat loading already in progress, ignoring duplicate request");
                return;
            }

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsLoading = true;
                    if (isRefresh)
                    {
                        IsRefreshing = true;
                    }
                });

                _logger.LogInformation("Loading chats (refresh: {IsRefresh})", isRefresh);

                var chats = await _chatService.GetChatsAsync();

                // Apply known online statuses from our cache
                await ApplyOnlineStatusesToChatsAsync(chats);

                // If we have chats, prepare them for display
                if (chats != null && chats.Count > 0)
                {
                    // Sort chats by last message time
                    var sortedChats = chats
                        .OrderByDescending(c => c.LastMessageTime ?? DateTime.MinValue)
                        .ToList();

                    // Update the UI
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Chats = new ObservableCollection<ChatModel>(sortedChats);
                        FilterChats();
                    });
                }
                else
                {
                    _logger.LogWarning("No chats returned");
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Chats = new ObservableCollection<ChatModel>();
                        FilterChats();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chats");
                await _toastService.ShowToastAsync($"Error loading chats: {ex.Message}", ToastType.Error);
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsLoading = false;
                    IsRefreshing = false;
                });

                _loadLock.Release();
            }
        }

        private async Task LoadOnlineUsersAsync()
        {
            try
            {
                // Get currently online users
                var onlineUsers = await _userService.GetOnlineUsersAsync();

                // Update the status cache
                if (onlineUsers != null)
                {
                    // First clear our cache
                    _userOnlineStatus.Clear();

                    // Then mark online users as online
                    foreach (var user in onlineUsers)
                    {
                        _userOnlineStatus[user.Id] = true;
                    }

                    _logger.LogInformation("Loaded {Count} online users", onlineUsers.Count);

                    // Apply the status to any loaded chats
                    await ApplyOnlineStatusesToChatsAsync(Chats);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load online users");
            }
        }

        private async Task ApplyOnlineStatusesToChatsAsync(IEnumerable<ChatModel> chats)
        {
            if (chats == null) return;

            bool updatedAny = false;

            foreach (var chat in chats)
            {
                if (chat.Participants != null)
                {
                    foreach (var participant in chat.Participants)
                    {
                        // فقط کاربر فعلی همیشه آنلاین است
                        if (participant.Id == await _chatService.GetCurrentUserIdAsync())
                        {
                            participant.IsOnline = true;
                            participant.LastActive = DateTime.UtcNow;
                            updatedAny = true;
                        }
                        // برای سایر کاربران، وضعیت واقعی را حفظ کن
                        else if (_userOnlineStatus.TryGetValue(participant.Id, out bool isOnline))
                        {
                            if (participant.IsOnline != isOnline)
                            {
                                participant.IsOnline = isOnline;
                                updatedAny = true;
                            }
                        }
                    }
                }
            }

            // اگر وضعیتی به‌روزرسانی شده، رابط کاربری را به‌روزرسانی کنیم
            if (updatedAny)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    OnPropertyChanged(nameof(Chats));
                    OnPropertyChanged(nameof(FilteredChats));
                });
            }
        }

        private void FilterChats()
        {
            try
            {
                if (string.IsNullOrEmpty(SearchQuery))
                {
                    FilteredChats = new ObservableCollection<ChatModel>(Chats);
                }
                else
                {
                    var query = SearchQuery.Trim().ToLowerInvariant();
                    var filteredList = Chats.Where(c =>
                        (c.DisplayTitle?.ToLowerInvariant().Contains(query) ?? false) ||
                        (c.LastMessage?.ToLowerInvariant().Contains(query) ?? false) ||
                        c.Participants.Any(p =>
                            (p.FirstName?.ToLowerInvariant().Contains(query) ?? false) ||
                            (p.LastName?.ToLowerInvariant().Contains(query) ?? false) ||
                            (p.PhoneNumber?.Contains(query) ?? false))
                    ).ToList();

                    FilteredChats = new ObservableCollection<ChatModel>(filteredList);
                }

                OnPropertyChanged(nameof(HasChats));
                OnPropertyChanged(nameof(IsEmpty));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering chats");
                // Show all chats in case of error
                FilteredChats = new ObservableCollection<ChatModel>(Chats);
            }
        }

        private async Task ChatSelectedAsync(ChatModel chat)
        {
            if (chat == null) return;

            try
            {
                _logger.LogInformation("Navigating to chat: {ChatId}", chat.Id);

                // Add timestamp to navigation parameters to ensure uniqueness each time
                var navigationParameter = new Dictionary<string, object>
                {
                    { "ChatId", chat.Id.ToString() },
                    { "t", DateTime.Now.Ticks.ToString() } // Timestamp for uniqueness
                };

                // Use relative path to avoid navigation stack issues
                await Shell.Current.GoToAsync($"{nameof(ChatPage)}", navigationParameter);

                // After navigating, mark chat as read in UI
                chat.UnreadCount = 0;
                OnPropertyChanged(nameof(FilteredChats));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to chat {ChatId}", chat.Id);
                await _toastService.ShowToastAsync($"Error opening chat: {ex.Message}", ToastType.Error);
            }
        }

        private void OnMessageReceived(MessageModel message)
        {
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Find the chat this message belongs to
                    var chat = Chats.FirstOrDefault(c => c.Id == message.ChatId);

                    if (chat != null)
                    {
                        // Update the chat with latest message info
                        chat.LastMessage = message.Content;
                        chat.LastMessageTime = message.SentAt;

                        // If this is not the user's own message, increment unread count
                        if (!message.IsOwnMessage)
                        {
                            chat.UnreadCount++;
                        }

                        // Re-sort chats to put this one at the top
                        var chatsList = Chats.ToList();
                        chatsList.Remove(chat);
                        chatsList.Insert(0, chat);

                        Chats = new ObservableCollection<ChatModel>(chatsList);
                        OnPropertyChanged(nameof(FilteredChats));
                    }
                    else
                    {
                        // This is a new chat, reload all chats
                        await LoadChatsAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing received message");
                }
            });
        }

        private void OnMessageRead(Guid chatId, int messageId)
        {
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Just refresh chats if needed
                    var chat = Chats.FirstOrDefault(c => c.Id == chatId);
                    if (chat != null)
                    {
                        await LoadChatsAsync(true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message read status");
                }
            });
        }

        private void OnUserStatusChanged(long userId, bool isOnline, DateTime? lastActive)
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    _logger.LogInformation("وضعیت کاربر {UserId} تغییر کرد: آنلاین = {IsOnline}, آخرین فعالیت = {LastActive}",
                        userId, isOnline, lastActive);

                    // یافتن چت‌های مرتبط با این کاربر
                    var affectedChats = Chats.Where(c =>
                        c.Participants.Any(p => p.Id == userId)
                    ).ToList();

                    if (affectedChats.Any())
                    {
                        // به‌روزرسانی وضعیت کاربر در هر چت
                        foreach (var chat in affectedChats)
                        {
                            var participant = chat.Participants.FirstOrDefault(p => p.Id == userId);
                            if (participant != null)
                            {
                                // وضعیت واقعی را نمایش بده
                                participant.IsOnline = isOnline;
                                participant.LastActive = lastActive;
                            }
                        }

                        // به‌روزرسانی رابط کاربری
                        OnPropertyChanged(nameof(Chats));
                        OnPropertyChanged(nameof(FilteredChats));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در به‌روزرسانی وضعیت کاربر");
                }
            });
        }

        public void RefreshChat(Guid chatId)
        {
            // Try to find and refresh a specific chat
            var chat = Chats.FirstOrDefault(c => c.Id == chatId);
            if (chat != null)
            {
                MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // Trigger a UI refresh
                    var index = Chats.IndexOf(chat);
                    if (index >= 0)
                    {
                        Chats[index] = chat;
                        OnPropertyChanged(nameof(FilteredChats));
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