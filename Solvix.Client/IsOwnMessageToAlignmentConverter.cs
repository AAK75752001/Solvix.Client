using System.Globalization;

namespace Solvix.Client
{
    public class IsOwnMessageToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isOwnMessage)
            {
                // For color conversions
                if (parameter != null && parameter.ToString() == "Color" &&
                    typeof(Color).IsAssignableFrom(targetType))
                {
                    try
                    {
                        return isOwnMessage
                            ? Application.Current.Resources["SentMessageBubbleColor"]
                            : Application.Current.Resources["ReceivedMessageBubbleColor"];
                    }
                    catch
                    {
                        // Fallback colors if resources not found
                        return isOwnMessage ? Colors.LightBlue : Colors.LightGray;
                    }
                }

                // For text color conversions
                if (parameter != null && parameter.ToString() == "TextColor" &&
                    typeof(Color).IsAssignableFrom(targetType))
                {
                    try
                    {
                        return isOwnMessage
                            ? Application.Current.Resources["SentMessageTextColor"]
                            : Application.Current.Resources["ReceivedMessageTextColor"];
                    }
                    catch
                    {
                        // Fallback colors if resources not found
                        return isOwnMessage ? Colors.Black : Colors.Black;
                    }
                }

                // For layout options
                if (targetType == typeof(LayoutOptions))
                {
                    return isOwnMessage ? LayoutOptions.End : LayoutOptions.Start;
                }
            }

            // Default fallbacks based on target type
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
