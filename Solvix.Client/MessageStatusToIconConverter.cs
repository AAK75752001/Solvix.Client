using System.Globalization;

namespace Solvix.Client
{
    public class MessageStatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                switch (status)
                {
                    case Core.Constants.MessageStatus.Failed:
                        return "\ue000"; // error
                    case Core.Constants.MessageStatus.Read:
                        return "\ue8f0"; // done_all (filled)
                    case Core.Constants.MessageStatus.Delivered:
                        return "\ue5ca"; // done_all (outline)
                    case Core.Constants.MessageStatus.Sent:
                        return "\ue5ca"; // done (outline)
                    case Core.Constants.MessageStatus.Sending:
                    default:
                        return "\ue192"; // watch_later
                }
            }

            return "\ue192"; // watch_later
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Core.Constants.MessageStatus.Sending;
        }
    }
}
