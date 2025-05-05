using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Solvix.Client.Core.Converters
{
    public class BoolToColorConverter : BindableObject, IValueConverter // از BindableObject ارث‌بری کن
    {
        // BindableProperty برای TrueColor
        public static readonly BindableProperty TrueColorProperty =
            BindableProperty.Create(nameof(TrueColor), typeof(Color), typeof(BoolToColorConverter), Colors.Green); // مقدار پیش‌فرض

        public Color TrueColor
        {
            get => (Color)GetValue(TrueColorProperty);
            set => SetValue(TrueColorProperty, value);
        }

        // BindableProperty برای FalseColor
        public static readonly BindableProperty FalseColorProperty =
            BindableProperty.Create(nameof(FalseColor), typeof(Color), typeof(BoolToColorConverter), Colors.Gray); // مقدار پیش‌فرض

        public Color FalseColor
        {
            get => (Color)GetValue(FalseColorProperty);
            set => SetValue(FalseColorProperty, value);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool condition = false;
            if (value is bool boolValue)
            {
                condition = boolValue;
            }

            // حالا از مقادیر ست شده در BindableProperty استفاده کن
            return condition ? TrueColor : FalseColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // تبدیل برعکس معمولا نیاز نیست
            if (value is Color color)
            {
                // یک منطق ساده برای برعکس (ممکنه دقیق نباشه)
                return color == TrueColor;
            }
            return false;
        }
    }
}