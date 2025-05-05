using System.Globalization;

namespace Solvix.Client.Core.Converters
{
    public class DateTimeToFormattedStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime dateTime)
            {
                return string.Empty;
            }

            var localDateTime = dateTime;
            var today = DateTime.Now.Date;


            try
            {
                if (localDateTime.Date == today)
                {
                    return localDateTime.ToString("HH:mm");
                }

                if (today.Subtract(localDateTime.Date).TotalDays < 7 && localDateTime.Date < today)
                {
                    return localDateTime.ToString("ddd");
                }


                return localDateTime.ToString("yyyy/MM/dd");

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error formatting date {dateTime}: {ex.Message}");
                return dateTime.ToString("yy/MM/dd");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}