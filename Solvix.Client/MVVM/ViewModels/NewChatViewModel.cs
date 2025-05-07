using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using Solvix.Client.MVVM.Views;
using System; // اطمینان از وجود using برای Guid, Exception, DateTime
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading; // Required for CancellationTokenSource
using System.Threading.Tasks;


namespace Solvix.Client.MVVM.ViewModels
{
    public partial class NewChatViewModel : ObservableObject
    {
        private readonly IUserService _userService;
        private readonly IChatService _chatService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        private readonly ILogger<NewChatViewModel> _logger;
        private long _currentUserId;
        private CancellationTokenSource? _searchDebounceCts;


        [ObservableProperty]
        private ObservableCollection<UserModel> _onlineUsersCache = new();

        [ObservableProperty]
        private ObservableCollection<UserModel> _filteredUsers = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSearchQuery))]
        private bool _hasPerformedSearch = false;

        public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

        public NewChatViewModel(
            IUserService userService,
            IChatService chatService,
            IToastService toastService,
            IAuthService authService,
            ILogger<NewChatViewModel> logger)
        {
            _userService = userService;
            _chatService = chatService;
            _toastService = toastService;
            _authService = authService;
            _logger = logger;

            PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(SearchQuery))
                {
                    await OnSearchQueryChanged();
                }
            };
        }

        private async Task InitializeAsync()
        {
            if (_currentUserId == 0)
            {
                _currentUserId = await _authService.GetUserIdAsync();
                if (_currentUserId == 0)
                {
                    _logger.LogError("Failed to get current user ID during initialization.");
                    await _toastService.ShowToastAsync("خطا در شناسایی کاربر فعلی.", ToastType.Error);
                }
            }
        }


        public async Task LoadDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            _logger.LogInformation("بارگذاری داده‌های صفحه چت جدید...");

            await InitializeAsync();
            if (_currentUserId == 0)
            {
                IsLoading = false;
                return;
            }

            try
            {
                var onlineUsersList = await _userService.GetOnlineUsersAsync();

                if (onlineUsersList != null && onlineUsersList.Any())
                {
                    OnlineUsersCache = new ObservableCollection<UserModel>(onlineUsersList.Where(u => u.Id != _currentUserId));
                    _logger.LogInformation("{Count} کاربر آنلاین (به جز کاربر فعلی) یافت شد", OnlineUsersCache.Count);
                }
                else
                {
                    OnlineUsersCache.Clear();
                    _logger.LogInformation("هیچ کاربر آنلاینی یافت نشد");
                }

                await ApplyFilterAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری لیست کاربران آنلاین");
                await _toastService.ShowToastAsync("خطا در بارگذاری لیست کاربران", ToastType.Error);
                OnlineUsersCache.Clear();
                FilteredUsers.Clear();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task OnSearchQueryChanged()
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();
            CancellationToken token = _searchDebounceCts.Token;

            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                await ApplyFilterAsync();
            }
            catch (TaskCanceledException)
            {
                _logger.LogTrace("Search debounce cancelled.");
            }
        }

        private async Task ApplyFilterAsync()
        {
            if (IsLoading && string.IsNullOrWhiteSpace(SearchQuery) && !_hasPerformedSearch) return;

            IsLoading = true;

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                HasPerformedSearch = false;
                FilteredUsers = new ObservableCollection<UserModel>(OnlineUsersCache);
                _logger.LogInformation("نمایش لیست کاربران آنلاین اولیه (فیلتر شده).");
            }
            else
            {
                _logger.LogInformation("جستجوی کاربران با عبارت: {Query}", SearchQuery);
                HasPerformedSearch = true;
                try
                {
                    var usersFromServer = await _userService.SearchUsersAsync(SearchQuery);
                    if (usersFromServer != null)
                    {
                        FilteredUsers = new ObservableCollection<UserModel>(usersFromServer.Where(u => u.Id != _currentUserId));
                        _logger.LogInformation("{Count} کاربر با جستجوی \"{Query}\" یافت شد", FilteredUsers.Count, SearchQuery);
                    }
                    else
                    {
                        FilteredUsers.Clear();
                        _logger.LogInformation("هیچ کاربری با جستجوی \"{Query}\" یافت نشد (پاسخ سرور null بود)", SearchQuery);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در جستجوی کاربران با عبارت \"{Query}\"", SearchQuery);
                    await _toastService.ShowToastAsync("خطا در جستجوی کاربران", ToastType.Error);
                    FilteredUsers.Clear();
                }
            }
            IsLoading = false;
        }


        [RelayCommand]
        private async Task SearchAsync()
        {
            await ApplyFilterAsync();
        }

        [RelayCommand]
        private async Task StartChatAsync(UserModel user)
        {
            if (user == null) return;
            if (_currentUserId == 0) await InitializeAsync();

            if (user.Id == _currentUserId)
            {
                await _toastService.ShowToastAsync("امکان شروع چت با خودتان وجود ندارد.", ToastType.Warning);
                return;
            }

            IsLoading = true;
            _logger.LogInformation("شروع چت با کاربر {UserName} (ID: {UserId})", user.DisplayName, user.Id);

            try
            {
                var result = await _chatService.StartChatWithUserAsync(user.Id);

                if (result.chatId.HasValue)
                {
                    _logger.LogInformation("چت با کاربر {UserName} ایجاد شد. ChatId: {ChatId}, موجود بود: {AlreadyExists}",
                        user.DisplayName, result.chatId, result.alreadyExists);
                    await Shell.Current.GoToAsync($"{nameof(ChatPage)}?ChatId={result.chatId}");
                }
                else
                {
                    _logger.LogError("خطا در ایجاد چت با کاربر {UserName}. ChatId از سرور دریافت نشد.", user.DisplayName);
                    await _toastService.ShowToastAsync("خطا در ایجاد چت. لطفاً مجدداً تلاش کنید.", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در شروع چت با کاربر {UserName}", user.DisplayName);
                await _toastService.ShowToastAsync($"خطا: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}