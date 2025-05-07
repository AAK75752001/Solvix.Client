using System.Globalization;
using Solvix.Client.Core.Helpers;

namespace Solvix.Client.Core.Converters
{
    public class MessageStatusToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                return MessageStatusHelper.GetStatusIconOpacity(status);
            }
            return 0.5; // Default opacity
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Constants.MessageStatus.Sending; // Default value for conversion back
        }
    }
}