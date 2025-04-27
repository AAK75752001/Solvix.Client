using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using Solvix.Client.MVVM.ViewModels;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace Solvix.Client.MVVM.ViewModels
{
    public class RegisterViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;

        private string _phoneNumber = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private bool _isLoading;
        private bool _isPhoneValid;
        private bool _isPasswordVisible;
        private bool _acceptedTerms;

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
                    OnPropertyChanged(nameof(PasswordsMatch));
                }
            }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (_confirmPassword != value)
                {
                    _confirmPassword = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                    OnPropertyChanged(nameof(PasswordsMatch));
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

        public bool AcceptedTerms
        {
            get => _acceptedTerms;
            set
            {
                if (_acceptedTerms != value)
                {
                    _acceptedTerms = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSubmitEnabled));
                }
            }
        }

        public bool PasswordsMatch => Password == ConfirmPassword && !string.IsNullOrEmpty(Password);

        public bool IsSubmitEnabled =>
            !IsLoading &&
            IsPhoneValid &&
            !string.IsNullOrEmpty(Password) &&
            Password.Length >= int.Parse(Constants.Validation.PasswordMinLength) &&
            PasswordsMatch &&
            AcceptedTerms;

        public ICommand RegisterCommand { get; private set; }
        public ICommand TogglePasswordVisibilityCommand { get; private set; }
        public ICommand BackCommand { get; private set; }

        public RegisterViewModel(IAuthService authService, IToastService toastService)
        {
            _authService = authService;
            _toastService = toastService;

            RegisterCommand = new Command(async () => await RegisterAsync());
            TogglePasswordVisibilityCommand = new Command(() => IsPasswordVisible = !IsPasswordVisible);
            BackCommand = new Command(async () => await GoBackAsync());
        }

        private bool ValidatePhoneNumber()
        {
            return !string.IsNullOrEmpty(PhoneNumber) && Regex.IsMatch(PhoneNumber, Constants.Validation.PhoneRegex);
        }

        private async Task RegisterAsync()
        {
            if (!IsSubmitEnabled)
                return;

            try
            {
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

