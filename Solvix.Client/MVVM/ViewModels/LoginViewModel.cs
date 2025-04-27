using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace Solvix.Client.MVVM.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;

        private string _phoneNumber = string.Empty;
        private string _password = string.Empty;
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private bool _isLoading;
        private bool _isPhoneChecking;
        private bool _isPhoneValid;
        private bool _isPasswordVisible;
        private bool _showPasswordField;
        private bool _showRegistrationFields;
        private bool _isLoginEnabled = true;

        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                if (_phoneNumber != value)
                {
                    _phoneNumber = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                    IsPhoneValid = ValidatePhoneNumber();
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        public string FirstName
        {
            get => _firstName;
            set
            {
                if (_firstName != value)
                {
                    _firstName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        public string LastName
        {
            get => _lastName;
            set
            {
                if (_lastName != value)
                {
                    _lastName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
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
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        public bool IsPhoneChecking
        {
            get => _isPhoneChecking;
            set
            {
                if (_isPhoneChecking != value)
                {
                    _isPhoneChecking = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        public bool IsPhoneValid
        {
            get => _isPhoneValid;
            set
            {
                if (_isPhoneValid != value)
                {
                    _isPhoneValid = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set
            {
                if (_isPasswordVisible != value)
                {
                    _isPasswordVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowPasswordField
        {
            get => _showPasswordField;
            set
            {
                if (_showPasswordField != value)
                {
                    _showPasswordField = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        public bool ShowRegistrationFields
        {
            get => _showRegistrationFields;
            set
            {
                if (_showRegistrationFields != value)
                {
                    _showRegistrationFields = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                    OnPropertyChanged(nameof(SubmitButtonText));
                }
            }
        }

        public bool IsLoginEnabled
        {
            get => _isLoginEnabled;
            set
            {
                if (_isLoginEnabled != value)
                {
                    _isLoginEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        public bool IsSubmitEnabled =>
            !IsLoading &&
            !IsPhoneChecking &&
            IsPhoneValid &&
            IsLoginEnabled &&
            (!ShowPasswordField || !string.IsNullOrEmpty(Password)) &&
            (!ShowRegistrationFields || (Password.Length >= int.Parse(Constants.Validation.PasswordMinLength)));

        public string SubmitButtonText => ShowRegistrationFields ? "Register" : (ShowPasswordField ? "Login" : "Continue");

        public ICommand SubmitCommand { get; private set; }
        public ICommand TogglePasswordVisibilityCommand { get; private set; }
        public ICommand ForgotPasswordCommand { get; private set; }

        public LoginViewModel(IAuthService authService, IToastService toastService)
        {
            _authService = authService;
            _toastService = toastService;

            SubmitCommand = new Command(async () => await SubmitAsync());
            TogglePasswordVisibilityCommand = new Command(() => IsPasswordVisible = !IsPasswordVisible);
            ForgotPasswordCommand = new Command(async () => await ForgotPasswordAsync());
        }

        private bool ValidatePhoneNumber()
        {
            return !string.IsNullOrEmpty(PhoneNumber) && Regex.IsMatch(PhoneNumber, Constants.Validation.PhoneRegex);
        }

        private async Task SubmitAsync()
        {
            if (!IsSubmitEnabled)
                return;

            if (!ShowPasswordField)
            {
                await CheckPhoneAndProceedAsync();
            }
            else if (ShowRegistrationFields)
            {
                await RegisterAsync();
            }
            else
            {
                await LoginAsync();
            }
        }

        private async Task CheckPhoneAndProceedAsync()
        {
            try
            {
                IsPhoneChecking = true;
                IsLoginEnabled = false;
                IsLoading = true;

                var exists = await _authService.CheckPhoneExists(PhoneNumber);

                // Show appropriate fields based on whether the phone exists
                ShowPasswordField = true;
                ShowRegistrationFields = !exists;

                if (exists)
                {
                    await _toastService.ShowToastAsync("Please enter your password to login", ToastType.Info);
                }
                else
                {
                    await _toastService.ShowToastAsync("Please complete registration", ToastType.Info);
                }
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Error: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsPhoneChecking = false;
                IsLoginEnabled = true;
                IsLoading = false;
            }
        }

        private async Task LoginAsync()
        {
            try
            {
                IsLoading = true;

                var user = await _authService.LoginAsync(PhoneNumber, Password);

                if (user != null)
                {
                    await _toastService.ShowToastAsync($"Welcome back, {user.DisplayName}!", ToastType.Success);

                    // Navigate to main page
                    Application.Current.MainPage = new AppShell();
                }
                else
                {
                    await _toastService.ShowToastAsync("Login failed. Please check your password.", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Error: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RegisterAsync()
        {
            try
            {
                if (Password.Length < int.Parse(Constants.Validation.PasswordMinLength))
                {
                    await _toastService.ShowToastAsync($"Password must be at least {Constants.Validation.PasswordMinLength} characters", ToastType.Warning);
                    return;
                }

                IsLoading = true;

                var registerDto = new RegisterDto
                {
                    PhoneNumber = PhoneNumber,
                    Password = Password,
                    FirstName = FirstName,
                    LastName = LastName
                };

                var user = await _authService.RegisterAsync(registerDto);

                if (user != null)
                {
                    await _toastService.ShowToastAsync("Registration successful!", ToastType.Success);

                    // Navigate to main page
                    Application.Current.MainPage = new AppShell();
                }
                else
                {
                    await _toastService.ShowToastAsync("Registration failed. Please try again.", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"Error: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ForgotPasswordAsync()
        {
            await _toastService.ShowToastAsync("Password reset will be available in a future update", ToastType.Info);
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