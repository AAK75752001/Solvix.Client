using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using Solvix.Client.MVVM.Views;
using System.Collections.ObjectModel;

namespace Solvix.Client.MVVM.ViewModels
{
    public partial class NewChatViewModel : ObservableObject
    {
        #region Services and Logger
        private readonly IUserService _userService;
        private readonly IChatService _chatService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        private readonly ILogger<NewChatViewModel> _logger;
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private ObservableCollection<UserModel> _allUsers = new();

        [ObservableProperty]
        private ObservableCollection<UserModel> _onlineUsers = new();

        [ObservableProperty]
        private ObservableCollection<UserModel> _filteredUsers = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSearchQuery))]
        private bool _hasPerformedSearch = false;

        // اضافه کردن مشخصه‌ای که نشان می‌دهد آیا جستجویی انجام شده یا خیر
        public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);
        #endregion

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

            // پردازش تغییرات در متن جستجو
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SearchQuery) && !IsLoading)
                {
                    // حذف لیست فیلتر شده در صورت خالی بودن عبارت جستجو
                    if (string.IsNullOrWhiteSpace(SearchQuery))
                    {
                        HasPerformedSearch = false;
                        FilteredUsers = new ObservableCollection<UserModel>(OnlineUsers);
                    }
                }
            };
        }

        #region Methods
        // بارگذاری داده‌ها در زمان ورود به صفحه
        public async Task LoadDataAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            _logger.LogInformation("بارگذاری داده‌های صفحه چت جدید...");

            try
            {
                // بارگذاری لیست کاربران آنلاین
                var onlineUsersList = await _userService.GetOnlineUsersAsync();

                if (onlineUsersList != null && onlineUsersList.Any())
                {
                    // به‌روزرسانی مجموعه کاربران آنلاین
                    OnlineUsers = new ObservableCollection<UserModel>(onlineUsersList);
                    _logger.LogInformation("{Count} کاربر آنلاین یافت شد", OnlineUsers.Count);

                    // در ابتدا، لیست فیلتر شده همان لیست کاربران آنلاین است
                    FilteredUsers = new ObservableCollection<UserModel>(OnlineUsers);
                }
                else
                {
                    OnlineUsers.Clear();
                    FilteredUsers.Clear();
                    _logger.LogInformation("هیچ کاربر آنلاینی یافت نشد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری لیست کاربران آنلاین");
                await _toastService.ShowToastAsync("خطا در بارگذاری لیست کاربران", ToastType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // جستجوی کاربران بر اساس نام یا شماره تلفن
        private async Task SearchUsersAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;

            IsLoading = true;
            _logger.LogInformation("جستجوی کاربران با عبارت: {Query}", SearchQuery);
            HasPerformedSearch = true;

            try
            {
                var users = await _userService.SearchUsersAsync(SearchQuery);

                if (users != null && users.Any())
                {
                    FilteredUsers = new ObservableCollection<UserModel>(users);
                    _logger.LogInformation("{Count} کاربر با جستجوی \"{Query}\" یافت شد", users.Count, SearchQuery);
                }
                else
                {
                    FilteredUsers.Clear();
                    _logger.LogInformation("هیچ کاربری با جستجوی \"{Query}\" یافت نشد", SearchQuery);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در جستجوی کاربران با عبارت \"{Query}\"", SearchQuery);
                await _toastService.ShowToastAsync("خطا در جستجوی کاربران", ToastType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task SearchAsync()
        {
            await SearchUsersAsync();
        }

        [RelayCommand]
        private async Task StartChatAsync(UserModel user)
        {
            if (user == null) return;

            IsLoading = true;
            _logger.LogInformation("شروع چت با کاربر {UserName} (ID: {UserId})", user.DisplayName, user.Id);

            try
            {
                // ایجاد چت جدید یا دریافت چت موجود با کاربر انتخاب شده
                var result = await _chatService.StartChatWithUserAsync(user.Id);

                if (result.chatId.HasValue)
                {
                    _logger.LogInformation("چت با کاربر {UserName} ایجاد شد. ChatId: {ChatId}, موجود بود: {AlreadyExists}",
                        user.DisplayName, result.chatId, result.alreadyExists);

                    // انتقال به صفحه چت
                    await Shell.Current.GoToAsync($"{nameof(ChatPage)}?ChatId={result.chatId}");
                }
                else
                {
                    _logger.LogError("خطا در ایجاد چت با کاربر {UserName}", user.DisplayName);
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
        #endregion
    }
}