using System.Globalization;
using Solvix.Client.Core.Helpers;

namespace Solvix.Client.Core.Converters
{
    public class MessageStatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                bool useEmoji = parameter?.ToString() == "UseEmoji";
                return MessageStatusHelper.GetStatusIcon(status, useEmoji);
            }
            return "schedule";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Constants.MessageStatus.Sending;
        }
    }
}