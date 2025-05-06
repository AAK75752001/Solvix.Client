using System.Globalization;

namespace Solvix.Client.Core.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public Color TrueColor { get; set; } = Colors.Green;
        public Color FalseColor { get; set; } = Colors.Gray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool condition = false;
            if (value is bool boolValue)
            {
                condition = boolValue;
            }

            string colorKey = condition ? "OnlineStatusColor" : "OfflineStatusColor";
            if (Application.Current?.Resources.TryGetValue(colorKey, out var resourceColor) == true && resourceColor is Color color)
            {
                return color;
            }

            return condition ? TrueColor : FalseColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false;
        }
    }
}