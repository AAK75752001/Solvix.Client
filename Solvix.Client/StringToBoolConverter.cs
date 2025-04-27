using System.Globalization;

namespace Solvix.Client
{
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;

            if (value is int intValue)
                return intValue > 0;

            if (value is string stringValue)
                return !string.IsNullOrWhiteSpace(stringValue);

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                if (parameter != null)
                    return parameter.ToString();

                return "True";
            }

            return string.Empty;
        }
    }
}
