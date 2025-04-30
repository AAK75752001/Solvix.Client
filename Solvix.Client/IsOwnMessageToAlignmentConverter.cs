using System.Globalization;

namespace Solvix.Client
{
    public class IsOwnMessageToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isOwnMessage)
            {
                // برای تبدیل رنگ
                if (parameter != null)
                {
                    string paramStr = parameter.ToString();

                    if (paramStr == "BgColor" && typeof(Color).IsAssignableFrom(targetType))
                    {
                        try
                        {
                            // برای رنگ زمینه پیام
                            return isOwnMessage
                                ? Application.Current.Resources["SentMessageBubbleColor"]
                                : Application.Current.Resources["ReceivedMessageBubbleColor"];
                        }
                        catch
                        {
                            // اگر رنگ‌ها در منابع پیدا نشد، رنگ‌های پیش‌فرض
                            return isOwnMessage ? Colors.LightBlue : Colors.LightGray;
                        }
                    }
                    else if (paramStr == "TextColor" && typeof(Color).IsAssignableFrom(targetType))
                    {
                        try
                        {
                            // برای رنگ متن پیام
                            return isOwnMessage
                                ? Application.Current.Resources["SentMessageTextColor"]
                                : Application.Current.Resources["ReceivedMessageTextColor"];
                        }
                        catch
                        {
                            // رنگ‌های پیش‌فرض
                            return isOwnMessage ? Colors.Black : Colors.Black;
                        }
                    }
                }

                // برای LayoutOptions
                if (targetType == typeof(LayoutOptions))
                {
                    return isOwnMessage ? LayoutOptions.End : LayoutOptions.Start;
                }
            }

            // مقادیر پیش‌فرض
            if (typeof(Color).IsAssignableFrom(targetType))
            {
                return Colors.Gray;
            }
            else if (targetType == typeof(LayoutOptions))
            {
                return LayoutOptions.Start;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false;
        }
    }
}
