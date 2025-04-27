using System.Globalization;

namespace Solvix.Client
{
    public class DateTimeToFormattedStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                // If it's today, show time
                if (dateTime.Date == DateTime.Today)
                    return dateTime.ToString("HH:mm");

                // If it's this week, show day name
                if (DateTime.Today.Subtract(dateTime.Date).TotalDays < 7)
                    return dateTime.ToString("ddd");

                // Older, show date
                return dateTime.ToString("yyyy-MM-dd");
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DateTime.Now;
        }
    }
}
