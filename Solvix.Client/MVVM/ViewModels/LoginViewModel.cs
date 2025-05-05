using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Solvix.Client.MVVM.ViewModels
{
    public partial class LoginViewModel : ObservableValidator
    {
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;

        // --- وضعیت‌های UI ---
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isPhoneNumberLocked;

        [ObservableProperty]
        private bool _showPasswordSection;

        [ObservableProperty]
        private bool _showRegistrationSection;

        [ObservableProperty]
        private bool _isPasswordVisible;

        // --- فیلدهای ورودی ---
        [ObservableProperty]
        [Required(ErrorMessage = "شماره تلفن الزامی است.")]
        [RegularExpression(Constants.Validation.PhoneRegex, ErrorMessage = "فرمت شماره تلفن نامعتبر است.")]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private string _phoneNumber = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "رمز عبور الزامی است.")]
        [MinLength(Constants.Validation.PasswordMinLength, ErrorMessage = "رمز عبور باید حداقل {1} کاراکتر باشد.")]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private string _password = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private string? _firstName;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private string? _lastName;

        // --- وضعیت فعلی ViewModel ---
        public enum LoginState { EnteringPhone, EnteringPassword, Registering }

        [ObservableProperty]
        private LoginState _currentState = LoginState.EnteringPhone;

        // --- Constructor ---
        public LoginViewModel(IAuthService authService, IToastService toastService)
        {
            _authService = authService;
            _toastService = toastService;
            ErrorsChanged += (s, e) => SubmitCommand.NotifyCanExecuteChanged();
        }

        // --- دستورات (Commands) ---
        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task SubmitAsync()
        {
            ValidateAllProperties();
            if (HasErrors)
            {
                var firstError = GetErrors().FirstOrDefault()?.ErrorMessage;
                await _toastService.ShowToastAsync(firstError ?? "لطفا خطاهای فرم را برطرف کنید.", ToastType.Warning);
                return;
            }

            IsLoading = true;
            try
            {
                switch (CurrentState)
                {
                    case LoginState.EnteringPhone:
                        await CheckPhoneAndProceedAsync();
                        break;
                    case LoginState.EnteringPassword:
                        await LoginAsync();
                        break;
                    case LoginState.Registering:
                        await RegisterAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"خطا: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanSubmit()
        {
            if (IsLoading) return false;

            ValidateAllProperties();

            switch (CurrentState)
            {
                case LoginState.EnteringPhone:
                    return !GetErrors(nameof(PhoneNumber)).Any() && !string.IsNullOrEmpty(PhoneNumber);
                case LoginState.EnteringPassword:
                    return !GetErrors(nameof(Password)).Any() && !string.IsNullOrEmpty(Password);
                case LoginState.Registering:
                    return !GetErrors(nameof(Password)).Any() && !string.IsNullOrEmpty(Password);
                default:
                    return false;
            }
        }


        [RelayCommand]
        private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

        [RelayCommand]
        private async Task ForgotPasswordAsync()
        {
            await _toastService.ShowToastAsync("قابلیت بازیابی رمز عبور به زودی اضافه خواهد شد.", ToastType.Info);
        }

        [RelayCommand]
        private void ChangePhoneNumber()
        {
            // Reset state to enter phone number again
            PhoneNumber = string.Empty;
            Password = string.Empty;
            FirstName = string.Empty;
            LastName = string.Empty;
            IsPhoneNumberLocked = false;
            ShowPasswordSection = false;
            ShowRegistrationSection = false;
            CurrentState = LoginState.EnteringPhone;
            ClearErrors();
            SubmitCommand.NotifyCanExecuteChanged();
        }

        // --- متدهای کمکی ---
        private async Task CheckPhoneAndProceedAsync()
        {
            try
            {
                var exists = await _authService.CheckPhoneExists(PhoneNumber);
                IsPhoneNumberLocked = true;
                ShowPasswordSection = true;

                if (exists)
                {
                    CurrentState = LoginState.EnteringPassword;
                    ShowRegistrationSection = false;
                    await _toastService.ShowToastAsync("لطفا رمز عبور خود را وارد کنید.", ToastType.Info);
                }
                else
                {
                    CurrentState = LoginState.Registering;
                    ShowRegistrationSection = true;
                    await _toastService.ShowToastAsync("این شماره ثبت نشده. لطفا اطلاعات ثبت‌نام را کامل کنید.", ToastType.Info);
                }
            }
            catch (Exception ex)
            {
                await _toastService.ShowToastAsync($"خطا در بررسی شماره: {ex.Message}", ToastType.Error);
                IsPhoneNumberLocked = false;
                ShowPasswordSection = false;
                ShowRegistrationSection = false;
                CurrentState = LoginState.EnteringPhone;
            }
            finally
            {
                // Trigger re-evaluation of CanExecute for the submit button
                SubmitCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task LoginAsync()
        {
            var user = await _authService.LoginAsync(PhoneNumber, Password);
            if (user != null)
            {
                await _toastService.ShowToastAsync($"خوش آمدید {user.DisplayName}!", ToastType.Success);
                // Navigate to main page
                Application.Current!.MainPage = new AppShell(); // Use non-null assertion if sure it's not null
            }
            else
            {
                await _toastService.ShowToastAsync("ورود ناموفق. لطفا رمز عبور را بررسی کنید.", ToastType.Error);
                // Password = string.Empty; // Optionally clear password field on failure
            }
        }

        private async Task RegisterAsync()
        {
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
                await _toastService.ShowToastAsync("ثبت نام با موفقیت انجام شد!", ToastType.Success);
                // Navigate to main page
                Application.Current!.MainPage = new AppShell(); // Use non-null assertion
            }
            else
            {
                await _toastService.ShowToastAsync("ثبت نام ناموفق. لطفا دوباره تلاش کنید.", ToastType.Error);
            }
        }

        // Helper for Validation Text
        public string? GetFirstError(string propertyName)
            => GetErrors(propertyName).FirstOrDefault()?.ErrorMessage;
    }
}