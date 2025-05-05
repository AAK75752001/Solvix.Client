using Solvix.Client.Core;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;


namespace Solvix.Client.MVVM.ViewModels
{
    public partial class LoginViewModel : ObservableValidator
    {
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ILogger<LoginViewModel> _logger;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private bool _isLoading;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private bool _isPhoneNumberLocked;

        [ObservableProperty]
        private bool _showPasswordSection;

        [ObservableProperty]
        private bool _showRegistrationSection;

        [ObservableProperty]
        private bool _isPasswordVisible;

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
        [Required(ErrorMessage = "وارد کردن نام الزامی است.")]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private string? _firstName;

        [ObservableProperty]
        [Required(ErrorMessage = "وارد کردن نام خانوادگی الزامی است.")]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private string? _lastName;

        public enum LoginState { EnteringPhone, EnteringPassword, Registering }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubmitButtonText))]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private LoginState _currentState = LoginState.EnteringPhone;

        public string SubmitButtonText => CurrentState switch
        {
            LoginState.EnteringPhone => "ادامه",
            LoginState.EnteringPassword => "ورود",
            LoginState.Registering => "ثبت نام",
            _ => "تایید"
        };

        public LoginViewModel(IAuthService authService, IToastService toastService, ILogger<LoginViewModel> logger)
        {
            _authService = authService;
            _toastService = toastService;
            _logger = logger;
        }

        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task SubmitAsync()
        {
            bool isValid = ValidateForCurrentState();
            if (!isValid)
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
                _logger.LogError(ex, "Error in SubmitAsync");
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
            return !HasErrorsForCurrentState();
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
            PhoneNumber = string.Empty;
            Password = string.Empty;
            FirstName = string.Empty;
            LastName = string.Empty;
            IsPhoneNumberLocked = false;
            ShowPasswordSection = false;
            ShowRegistrationSection = false;
            CurrentState = LoginState.EnteringPhone;
            ClearErrors();
        }

        private async Task CheckPhoneAndProceedAsync()
        {
            ClearErrors(nameof(Password));
            ClearErrors(nameof(FirstName));
            ClearErrors(nameof(LastName));

            IsLoading = true;
            IsPhoneNumberLocked = true;

            try
            {
                var exists = await _authService.CheckPhoneExists(PhoneNumber);
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
                _logger.LogError(ex, "Error in CheckPhoneAndProceedAsync for {PhoneNumber}", PhoneNumber);
                await _toastService.ShowToastAsync($"خطا در بررسی شماره: {ex.Message}", ToastType.Error);
                IsPhoneNumberLocked = false;
                ShowPasswordSection = false;
                ShowRegistrationSection = false;
                CurrentState = LoginState.EnteringPhone;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoginAsync()
        {
            var user = await _authService.LoginAsync(PhoneNumber, Password);
            if (user != null)
            {
                await _toastService.ShowToastAsync($"خوش آمدید {user.DisplayName}!", ToastType.Success);

                try
                {
                    _logger.LogInformation("Setting MainPage to AppShell after successful login.");
                    Application.Current!.MainPage = new AppShell();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting MainPage to AppShell after login.");
                    await _toastService.ShowToastAsync("خطا در نمایش صفحه اصلی.", ToastType.Error);
                }
            }
            else
            {
                await _toastService.ShowToastAsync("ورود ناموفق. لطفا رمز عبور را بررسی کنید.", ToastType.Error);
                Password = string.Empty;
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

                try
                {
                    _logger.LogInformation("Setting MainPage to AppShell after successful registration.");
                    Application.Current!.MainPage = new AppShell();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting MainPage to AppShell after registration.");
                    await _toastService.ShowToastAsync("خطا در نمایش صفحه اصلی.", ToastType.Error);
                }
            }
            else
            {
                await _toastService.ShowToastAsync("ثبت نام ناموفق. لطفا دوباره تلاش کنید.", ToastType.Error);
            }
        }

        private bool ValidateForCurrentState()
        {
            ClearErrors();
            bool isValid = true;

            if (!IsPhoneNumberLocked)
            {
                ValidateProperty(PhoneNumber, nameof(PhoneNumber));
                if (GetErrors(nameof(PhoneNumber)).Any()) isValid = false;
            }

            if (CurrentState == LoginState.EnteringPassword || CurrentState == LoginState.Registering)
            {
                ValidateProperty(Password, nameof(Password));
                if (GetErrors(nameof(Password)).Any()) isValid = false;
            }

            if (CurrentState == LoginState.Registering)
            {
                ValidateProperty(FirstName, nameof(FirstName));
                if (GetErrors(nameof(FirstName)).Any()) isValid = false;

                ValidateProperty(LastName, nameof(LastName));
                if (GetErrors(nameof(LastName)).Any()) isValid = false;
            }

            SubmitCommand.NotifyCanExecuteChanged();
            return isValid;
        }

        private bool HasErrorsForCurrentState()
        {
            if (CurrentState == LoginState.EnteringPhone)
            {
                return GetErrors(nameof(PhoneNumber)).Any();
            }
            if (CurrentState == LoginState.EnteringPassword)
            {
                return GetErrors(nameof(PhoneNumber)).Any() || GetErrors(nameof(Password)).Any();
            }
            if (CurrentState == LoginState.Registering)
            {
                return GetErrors(nameof(PhoneNumber)).Any() || GetErrors(nameof(Password)).Any()
                    || GetErrors(nameof(FirstName)).Any() || GetErrors(nameof(LastName)).Any();
            }
            return true;
        }
    }
}