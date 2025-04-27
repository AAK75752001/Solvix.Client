using System.Globalization;

namespace Solvix.Client
{
    public class IsOwnMessageToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isOwnMessage)
            {
                if (parameter != null && parameter.ToString() == "Color")
                {
                    return isOwnMessage
                        ? Application.Current.Resources["SentMessageBubbleColor"]
                        : Application.Current.Resources["ReceivedMessageBubbleColor"];
                }

                if (parameter != null && parameter.ToString() == "TextColor")
                {
                    return isOwnMessage
                        ? Application.Current.Resources["SentMessageTextColor"]
                        : Application.Current.Resources["ReceivedMessageTextColor"];
                }

                if (parameter != null && parameter.ToString() == "SecondaryTextColor")
                {
                    // Fix for color conversion issue
                    if (Application.Current.Resources["SentMessageTextColor"] is Color sentColor &&
                        Application.Current.Resources["ReceivedMessageTextColor"] is Color receivedColor)
                    {
                        return isOwnMessage
                            ? new Color(sentColor.Red, sentColor.Green, sentColor.Blue, 0.7f)
                            : new Color(receivedColor.Red, receivedColor.Green, receivedColor.Blue, 0.7f);
                    }
                }

                return isOwnMessage ? LayoutOptions.End : LayoutOptions.Start;
            }

            return LayoutOptions.Start;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false;
        }
    }
}
