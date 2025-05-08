using System.Globalization;


namespace Solvix.Client.Core.Converters
{
    public class ConnectionStateTitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isConnected)
            {
                return isConnected ? "سالویکس" : "در حال اتصال به سالویکس...";
            }

            return "سالویکس";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
