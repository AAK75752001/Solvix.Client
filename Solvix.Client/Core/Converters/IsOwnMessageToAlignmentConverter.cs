using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Solvix.Client.Core.Converters
{
    public class IsOwnMessageToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOwnMessage = value is bool b && b;
            string? paramStr = parameter as string;

            // برای HorizontalOptions
            if (targetType == typeof(LayoutOptions))
            {
                return isOwnMessage ? LayoutOptions.End : LayoutOptions.Start;
            }

            // برای BackgroundColor
            if (paramStr == "BgColor")
            {
                string resourceKey = isOwnMessage ? "SentMessageBubbleColor" : "ReceivedMessageBubbleColor";
                if (Application.Current != null && Application.Current.Resources.TryGetValue(resourceKey, out var resColor) && resColor is Color color)
                {
                    return color;
                }
                return isOwnMessage ? Color.FromArgb("#E1D2FF") : Colors.White;
            }

            // برای TextColor
            if (paramStr == "TextColor")
            {
                string resourceKey = isOwnMessage ? "SentMessageTextColor" : "ReceivedMessageTextColor";
                if (Application.Current != null && Application.Current.Resources.TryGetValue(resourceKey, out var resColor) && resColor is Color color)
                {
                    return color;
                }
                return Color.FromArgb("#151515");
            }

            // برای CornerRadius حباب
            if (paramStr == "Corners")
            {
                return isOwnMessage ? new CornerRadius(15, 15, 5, 15) : new CornerRadius(15, 15, 15, 5);
            }

            // Fallback پیش‌فرض
            return isOwnMessage ? LayoutOptions.End : LayoutOptions.Start;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}