using System.Globalization;
using Solvix.Client.Core;

namespace Solvix.Client
{
    public class MessageStatusIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                // تبدیل وضعیت به آیکون مناسب
                switch (status)
                {
                    case Constants.MessageStatus.Failed:
                        return "❌"; // خطا در ارسال
                    case Constants.MessageStatus.Sending:
                        return "⏱"; // در حال ارسال
                    case Constants.MessageStatus.Read:
                        return "✓✓"; // خوانده شده (دو تیک)
                    case Constants.MessageStatus.Delivered:
                        return "✓"; // تحویل داده شده (یک تیک)
                    case Constants.MessageStatus.Sent:
                        return "✓"; // ارسال شده (یک تیک)
                    default:
                        return "⏱"; // حالت پیش‌فرض
                }
            }

            // مقدار پیش‌فرض
            return "⏱";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Constants.MessageStatus.Sending; // مقدار پیش‌فرض برگشت
        }
    }

    public class MessageStatusOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                // شفافیت متفاوت برای وضعیت‌های مختلف
                switch (status)
                {
                    case Constants.MessageStatus.Read:
                        return 1.0; // شفافیت کامل برای خوانده شده
                    case Constants.MessageStatus.Delivered:
                    case Constants.MessageStatus.Sent:
                        return 0.7; // کمی تیره برای تحویل داده شده و ارسال شده
                    case Constants.MessageStatus.Failed:
                        return 1.0; // شفافیت کامل برای خطا
                    default:
                        return 0.5; // تیره برای در حال ارسال و دیگر حالت‌ها
                }
            }

            // شفافیت پیش‌فرض
            return 0.5;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Constants.MessageStatus.Sending; // مقدار پیش‌فرض برگشت
        }
    }
}