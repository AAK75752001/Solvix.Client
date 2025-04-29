using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solvix.Client
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue && parameter is string paramStr)
            {
                switch (paramStr)
                {
                    case "Error":
                        return Colors.Red;
                    case "Success":
                        return Colors.Green;
                    case "Warning":
                        return Colors.Orange;
                    case "Info":
                        return Colors.Blue;
                }
            }

            // حالت پیش‌فرض بر اساس تم فعلی
            try
            {
                return Application.Current.Resources["SecondaryTextColor"];
            }
            catch
            {
                return Colors.Gray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false;
        }
    }
}
