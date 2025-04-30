using System.Globalization;
using Solvix.Client.Core.Helpers;

namespace Solvix.Client
{
    public class MessageStatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                bool useEmoji = parameter?.ToString() != "IconCode";
                return MessageStatusHelper.GetStatusIcon(status, useEmoji);
            }

            // Default value
            return "⏱"; // Watch icon as fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Core.Constants.MessageStatus.Sending;
        }
    }

    public class MessageStatusToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                return MessageStatusHelper.GetStatusIconOpacity(status);
            }

            // Default opacity
            return 0.5;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Core.Constants.MessageStatus.Sending;
        }
    }
}