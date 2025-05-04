using System.Globalization;

namespace Solvix.Client
{
    public class IsOwnMessageToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOwnMessage = value is bool boolValue && boolValue;

            if (targetType == typeof(LayoutOptions))
            {
                return isOwnMessage ? LayoutOptions.End : LayoutOptions.Start;
            }

            if (typeof(Color).IsAssignableFrom(targetType))
            {
                if (parameter is string paramStr)
                {
                    if (paramStr == "BgColor")
                    {
                        try
                        {
                            if (isOwnMessage)
                            {
                                if (Application.Current?.Resources?.TryGetValue("SentMessageBubbleColor", out var sentColor) == true)
                                    return (Color)sentColor;
                                return Colors.LightBlue;
                            }
                            else
                            {
                                if (Application.Current?.Resources?.TryGetValue("ReceivedMessageBubbleColor", out var receivedColor) == true)
                                    return (Color)receivedColor;
                                return Colors.LightGray;
                            }
                        }
                        catch
                        {
                            return isOwnMessage ? Colors.LightBlue : Colors.LightGray;
                        }
                    }
                    else if (paramStr == "TextColor")
                    {
                        try
                        {
                            if (isOwnMessage)
                            {
                                if (Application.Current?.Resources?.TryGetValue("SentMessageTextColor", out var sentTextColor) == true)
                                    return (Color)sentTextColor;
                                return Colors.Black;
                            }
                            else
                            {
                                if (Application.Current?.Resources?.TryGetValue("ReceivedMessageTextColor", out var receivedTextColor) == true)
                                    return (Color)receivedTextColor;
                                return Colors.Black;
                            }
                        }
                        catch
                        {
                            return Colors.Black;
                        }
                    }
                }

                return Colors.Gray;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false;
        }
    }
}