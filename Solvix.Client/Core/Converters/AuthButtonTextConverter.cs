using Solvix.Client.MVVM.ViewModels;
using System.Globalization;

namespace Solvix.Client.Core.Converters
{
    public class AuthButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LoginViewModel.LoginState state)
            {
                return state switch
                {
                    LoginViewModel.LoginState.EnteringPhone => "Continue",
                    LoginViewModel.LoginState.EnteringPassword => "Login",
                    LoginViewModel.LoginState.Registering => "Register",
                    _ => "Submit"
                };
            }
            return "Submit";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
