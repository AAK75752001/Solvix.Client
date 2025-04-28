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

                // For alignment, explicitly return LayoutOptions.End for own messages, Start for others
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
