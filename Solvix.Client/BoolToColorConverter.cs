using System.Globalization;

namespace Solvix.Client
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool condition = false;

            if (value is bool boolValue)
            {
                condition = boolValue;
            }
            else if (value is string strValue)
            {
                bool.TryParse(strValue, out condition);
            }

            if (condition && parameter is string paramStr)
            {
                return paramStr switch
                {
                    "Error" => Colors.Red,
                    "Success" => Colors.Green,
                    "Warning" => Colors.Orange,
                    "Info" => Colors.Blue,
                    _ => Colors.Gray
                };
            }

            try
            {
                if (Application.Current?.Resources?.TryGetValue("SecondaryTextColor", out var color) == true)
                {
                    return (Color)color;
                }
            }
            catch { }

            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false;
        }
    }
}