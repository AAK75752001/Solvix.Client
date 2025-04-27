using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using Solvix.Client.MVVM.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Solvix.Client.MVVM.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ISignalRService _signalRService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MainViewModel> _logger;

        private UserModel _currentUser;
        private int _selectedTabIndex = 0;
        private bool _isInitializing = true;

        public UserModel CurrentUser
        {
            get => _currentUser;
            set
            {
                if (_currentUser != value)
                {
                    _currentUser = value;
                    OnPropertyChanged();
                }
            }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsInitializing
        {
            get => _isInitializing;
            set
            {
                if (_isInitializing != value)
                {
                    _isInitializing = value;
                    OnPropertyChanged();
                }
            }
        }

        public ChatListViewModel ChatListViewModel { get; }

        public ICommand NewChatCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand LogoutCommand { get; }

        public MainViewModel(
            IAuthService authService,
            IToastService toastService,
            ISignalRService signalRService,
            IServiceProvider serviceProvider,
            ChatListViewModel chatListViewModel,
            ILogger<MainViewModel> logger)
        {
            _authService = authService;
            _toastService = toastService;
            _signalRService = signalRService;
            _serviceProvider = serviceProvider;
            _logger = logger;

            ChatListViewModel = chatListViewModel;

            NewChatCommand = new Command(async () => await NewChatAsync());
            SettingsCommand = new Command(async () => await GoToSettingsAsync());
            LogoutCommand = new Command(async () => await LogoutAsync());

            // Initialize a default user to avoid null references
            _currentUser = new UserModel
            {
                Id = 0,
                Username = "Loading...",
                FirstName = "Loading",
                LastName = "..."
            };

            // Initialize the view model asynchronously
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                IsInitializing = true;

                // Load user info
                await LoadUserAsync();

                // Allow UI to render even if SignalR fails
                Task connectTask = ConnectToSignalRAsync();

                // Don't wait for SignalR
                IsInitializing = false;

                // Continue SignalR connection in background
                await connectTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing MainViewModel");
                IsInitializing = false;
            }
        }

        private async Task LoadUserAsync()
        {
            try
            {
                _logger.LogInformation("Loading user information");
                var user = await _authService.GetCurrentUserAsync();

                if (user != null)
                {
                    CurrentUser = user;
                    _logger.LogInformation("User loaded: {DisplayName}", user.DisplayName);
                }
                else
                {
                    _logger.LogWarning("Failed to load user information, using default values");
                    // Keep the default "Loading..." user
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await _toastService.ShowToastAsync("Failed to load user information", ToastType.Warning);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user data");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await _toastService.ShowToastAsync("Error loading user data", ToastType.Error);
                });
            }
        }

        private async Task ConnectToSignalRAsync()
        {
            try
            {
                _logger.LogInformation("Attempting to connect to SignalR");

                // Use a timeout for the connection attempt
                var connectTask = _signalRService.ConnectAsync();
                var timeoutTask = Task.Delay(5000); // 5 second timeout

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("SignalR connection timed out - will retry in background");
                    // Let the connection continue in the background
                    // The SignalR service has its own retry logic
                }
                else
                {
                    _logger.LogInformation("Successfully connected to SignalR");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to SignalR");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await _toastService.ShowToastAsync("Failed to connect to chat service - some features may be limited", ToastType.Warning);
                });
            }
        }

        private async Task NewChatAsync()
        {
            await Shell.Current.GoToAsync(nameof(NewChatPage));
        }

        private async Task GoToSettingsAsync()
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }

        private async Task LogoutAsync()
        {
            bool confirm = await Shell.Current.CurrentPage.DisplayAlert(
                "Logout",
                "Are you sure you want to logout?",
                "Yes",
                "No");

            if (confirm)
            {
                try
                {
                    await _signalRService.DisconnectAsync();
                    await _authService.LogoutAsync();

                    // Use service provider to resolve LoginPage with its dependencies
                    var loginPage = _serviceProvider.GetService<LoginPage>();
                    Application.Current.MainPage = new NavigationPage(loginPage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Logout failed");
                    await _toastService.ShowToastAsync($"Logout failed: {ex.Message}", ToastType.Error);
                }
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