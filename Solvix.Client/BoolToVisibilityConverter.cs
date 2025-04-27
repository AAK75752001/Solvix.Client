using System.Globalization;

namespace Solvix.Client
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter is string paramStr)
                {
                    if (paramStr == "Color")
                    {
                        try
                        {
                            // اگر آنلاین است رنگ سبز و در غیر این صورت رنگ خاکستری
                            return boolValue
                                ? Application.Current.Resources["OnlineStatusColor"]
                                : Application.Current.Resources["OfflineStatusColor"];
                        }
                        catch
                        {
                            // اگر رنگ‌ها در منابع پیدا نشدند، رنگ‌های پیش‌فرض
                            return boolValue ? Colors.Green : Colors.Gray;
                        }
                    }
                    else if (paramStr == "Text")
                    {
                        return boolValue ? "●" : "○"; // دایره توپر برای آنلاین، دایره خالی برای آفلاین
                    }
                }
                return boolValue;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }

            return false;
        }
    }
}
