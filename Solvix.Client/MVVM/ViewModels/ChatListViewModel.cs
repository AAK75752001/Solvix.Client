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
    public partial class ChatListViewModel : ObservableObject
    {
        private readonly IChatService _chatService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        private readonly ILogger<ChatListViewModel> _logger;

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


        public ChatListViewModel(
            IChatService chatService,
            IToastService toastService,
             IAuthService authService,
             ILogger<ChatListViewModel> logger)

        {
            _chatService = chatService;
            _toastService = toastService;
            _authService = authService;
            _logger = logger;

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SearchQuery))
                {
                    FilterChats();
                }
            };
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
                var chatList = await _chatService.GetUserChatsAsync();

                if (chatList != null)
                {
                    _allChats = chatList.OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt).ToList();
                    long currentUserId = await _authService.GetUserIdAsync();

                    foreach (var chat in _allChats)
                    {
                        CalculateOtherParticipant(chat, currentUserId);
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


        private void CalculateOtherParticipant(ChatModel chat, long currentUserId)
        {
            if (!chat.IsGroup && chat.Participants != null && chat.Participants.Any())
            {
                chat.OtherParticipant = chat.Participants.FirstOrDefault(p => p.Id != currentUserId);
            }
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
                    (c.OtherParticipant?.PhoneNumber != null && c.OtherParticipant.PhoneNumber.Contains(query)) // جستجو در شماره تلفن مخاطب
                );
            }

            var chatsToRemove = FilteredChats.Except(chatsToShow).ToList();
            foreach (var chat in chatsToRemove) { FilteredChats.Remove(chat); }

            var chatsToAdd = chatsToShow.Except(FilteredChats).ToList();
            foreach (var chat in chatsToAdd.OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt))
            {
                int index = chatsToShow.ToList().FindIndex(c => c.Id == chat.Id);
                if (index >= 0 && index < FilteredChats.Count)
                    FilteredChats.Insert(index, chat);
                else
                    FilteredChats.Add(chat);
            }

            var sorted = FilteredChats.OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt).ToList();
            if (!FilteredChats.SequenceEqual(sorted))
            {
                FilteredChats = new ObservableCollection<ChatModel>(sorted);
                OnPropertyChanged(nameof(FilteredChats));
            }

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
            // await KeyboardExtensions.HideKeyboardAsync( CancellationToken.None);
        }

        [RelayCommand]
        private async Task NewChatAsync()
        {
            _logger.LogInformation("New Chat command executed.");
            await _toastService.ShowToastAsync("شروع چت جدید (به زودی!)", ToastType.Info);
            // await _navigationService.NavigateToAsync(nameof(NewChatPage));
        }

        [RelayCommand]
        private async Task GoToSettingsAsync()
        {
            _logger.LogInformation("Go To Settings command executed.");
            await _toastService.ShowToastAsync("رفتن به تنظیمات (به زودی!)", ToastType.Info);
            // await _navigationService.NavigateToAsync(nameof(SettingsPage));
        }

        public async Task OnAppearingAsync()
        {
            _logger.LogInformation("ChatListPage appearing. Loading chats...");
            await LoadChatsAsync();
        }
    }
}