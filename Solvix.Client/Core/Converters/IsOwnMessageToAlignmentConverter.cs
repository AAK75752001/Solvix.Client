using System.Globalization;


namespace Solvix.Client.Core.Converters
{
    public class IsOwnMessageToAlignmentConverter : IValueConverter
    {
        public Color SentBubbleColor { get; set; } = Color.FromArgb("#E1D2FF");
        public Color ReceivedBubbleColor { get; set; } = Colors.White;
        public Color SentTextColor { get; set; } = Color.FromArgb("#151515");
        public Color ReceivedTextColor { get; set; } = Color.FromArgb("#151515");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOwnMessage = value is bool b && b;

            if (targetType == typeof(LayoutOptions))
            {
                return isOwnMessage ? LayoutOptions.End : LayoutOptions.Start;
            }

            if (typeof(Color).IsAssignableFrom(targetType))
            {
                string? paramStr = parameter as string;

                if (paramStr == "BgColor")
                {
                    string resourceKey = isOwnMessage ? "SentMessageBubbleColor" : "ReceivedMessageBubbleColor";
                    if (Application.Current != null && Application.Current.Resources.TryGetValue(resourceKey, out var resColor) && resColor is Color color)
                    {
                        return color;
                    }
                    return isOwnMessage ? SentBubbleColor : ReceivedBubbleColor;
                }
                else if (paramStr == "TextColor")
                {
                    string resourceKey = isOwnMessage ? "SentMessageTextColor" : "ReceivedMessageTextColor";
                    if (Application.Current != null && Application.Current.Resources.TryGetValue(resourceKey, out var resColor) && resColor is Color color)
                    {
                        return color;
                    }
                    return isOwnMessage ? SentTextColor : ReceivedTextColor;
                }
            }

            return isOwnMessage ? LayoutOptions.End : LayoutOptions.Start;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}