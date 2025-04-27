using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using Solvix.Client.MVVM.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Solvix.Client.MVVM.ViewModels
{
    public class NewChatViewModel : INotifyPropertyChanged
    {
        private readonly IUserService _userService;
        private readonly IChatService _chatService;
        private readonly IToastService _toastService;

        private string _searchQuery = string.Empty;
        private bool _isSearching;
        private ObservableCollection<UserModel> _users = new();
        private ObservableCollection<UserModel> _onlineUsers = new();

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSearch));
                }
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (_isSearching != value)
                {
                    _isSearching = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSearch));
                }
            }
        }

        public ObservableCollection<UserModel> Users
        {
            get => _users;
            set
            {
                if (_users != value)
                {
                    _users = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasUsers));
                }
            }
        }

        public ObservableCollection<UserModel> OnlineUsers
        {
            get => _onlineUsers;
            set
            {
                if (_onlineUsers != value)
                {
                    _onlineUsers = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasOnlineUsers));
                }
            }
        }

        public bool CanSearch => !string.IsNullOrWhiteSpace(SearchQuery) && !IsSearching;

        public bool HasUsers => Users.Count > 0;

        public bool HasOnlineUsers => OnlineUsers.Count > 0;

        public bool IsEmpty => !IsSearching && !HasUsers && string.IsNullOrWhiteSpace(SearchQuery);

        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand SelectUserCommand { get; }
        public ICommand BackCommand { get; }

        public NewChatViewModel(IUserService userService, IChatService chatService, IToastService toastService)
        {
            _userService = userService;
            _chatService = chatService;
            _toastService = toastService;

            SearchCommand = new Command(async () => await SearchUsersAsync());
            ClearSearchCommand = new Command(ClearSearch);
            SelectUserCommand = new Command<UserModel>(async (user) => await SelectUserAsync(user));
            BackCommand = new Command(async () => await GoBackAsync());

            // Initial load of online users
            LoadOnlineUsersAsync().ConfigureAwait(false);
        }

        private async Task SearchUsersAsync()
        {
            if (!CanSearch)
                return;

            try
            {
                IsSearching = true;

                var users = await _userService.SearchUsersAsync(SearchQuery);

                if (users != null)
                {
                    Users = new ObservableCollection<UserModel>(users);
                }

                OnPropertyChanged(nameof(HasUsers));
                OnPropertyChanged(nameof(IsEmpty));
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Error searching users: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsSearching = false;
            }
        }

        private async Task LoadOnlineUsersAsync()
        {
            try
            {
                var users = await _userService.GetOnlineUsersAsync();

                if (users != null)
                {
                    OnlineUsers = new ObservableCollection<UserModel>(users);
                }

                OnPropertyChanged(nameof(HasOnlineUsers));
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Error loading online users: {ex.Message}", ToastType.Error);
            }
        }

        private void ClearSearch()
        {
            SearchQuery = string.Empty;
            Users.Clear();
            OnPropertyChanged(nameof(HasUsers));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private async Task SelectUserAsync(UserModel user)
        {
            if (user == null)
                return;

            try
            {
                await _toastService.ShowToastAsync($"Starting chat with {user.DisplayName}...", ToastType.Info);

                // Start a chat with this user
                var chatId = await _chatService.StartChatAsync(user.Id);

                if (chatId.HasValue)
                {
                    // Navigate to the chat page
                    var navigationParameter = new Dictionary<string, object>
                    {
                        { "ChatId", chatId.Value }
                    };

                    await Shell.Current.GoToAsync($"{nameof(ChatPage)}", navigationParameter);
                }
                else
                {
                    await _toastService.ShowToastAsync("Failed to start chat", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Error: {ex.Message}", ToastType.Error);
            }
        }

        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
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