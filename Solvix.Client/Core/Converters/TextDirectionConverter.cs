using System.Globalization;

namespace Solvix.Client.Core.Converters
{
    public class TextDirectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                // اگر متن خالی باشد، راست به چپ (پیش‌فرض برای فارسی)
                if (string.IsNullOrEmpty(text))
                    return FlowDirection.RightToLeft;

                // بررسی اولین کاراکتر غیر فاصله
                for (int i = 0; i < text.Length; i++)
                {
                    if (char.IsWhiteSpace(text[i]))
                        continue;

                    // بررسی اینکه آیا کاراکتر فارسی/عربی است
                    // محدوده حروف فارسی/عربی در یونیکد
                    if ((text[i] >= 0x0600 && text[i] <= 0x06FF) ||   // Persian/Arabic
                        (text[i] >= 0xFB50 && text[i] <= 0xFDFF) ||   // Arabic presentation forms-A
                        (text[i] >= 0xFE70 && text[i] <= 0xFEFF))     // Arabic presentation forms-B
                    {
                        return FlowDirection.RightToLeft;
                    }
                    else
                    {
                        return FlowDirection.LeftToRight;
                    }
                }
            }

            return FlowDirection.RightToLeft;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}