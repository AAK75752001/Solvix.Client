using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solvix.Client.Resources.Themes
{
    public partial class SolvixThemeResources : ResourceDictionary
    {
        public SolvixThemeResources()
        {
            // رنگ‌های اصلی تم سالویکس - پالت رنگی قدرتمند و مدرن
            this["PrimaryColor"] = Color.FromArgb("#6200EA");       // بنفش عمیق و غنی
            this["SecondaryColor"] = Color.FromArgb("#00C4B4");     // فیروزه‌ای روشن
            this["TertiaryColor"] = Color.FromArgb("#4A0ABF");      // بنفش تیره‌تر
            this["AccentColor"] = Color.FromArgb("#FF5E76");        // صورتی گرم

            // پس‌زمینه و کارت‌ها
            this["PageBackgroundColor"] = Color.FromArgb("#F8F9FC"); // سفید کمی مایل به آبی خیلی روشن
            this["CardBackgroundColor"] = Color.FromArgb("#FFFFFF"); // سفید خالص
            this["FrameBorderColor"] = Color.FromArgb("#EAEEF8");   // خاکستری مایل به آبی روشن

            // رنگ‌های متن
            this["PrimaryTextColor"] = Color.FromArgb("#101C3D");   // سورمه‌ای تیره
            this["SecondaryTextColor"] = Color.FromArgb("#4A5578");  // سورمه‌ای متوسط
            this["TertiaryTextColor"] = Color.FromArgb("#8D93A8");   // سورمه‌ای روشن
            this["InverseTextColor"] = Colors.White;                // سفید

            // رنگ‌های کاربردی
            this["SeparatorColor"] = Color.FromArgb("#EAEEF8");     // خاکستری مایل به آبی روشن
            this["ShadowColor"] = Color.FromArgb("#25101C3D");      // سایه با کمی شفافیت
            this["SuccessColor"] = Color.FromArgb("#00C48C");       // سبز روشن
            this["ErrorColor"] = Color.FromArgb("#FF5E5E");         // قرمز روشن
            this["WarningColor"] = Color.FromArgb("#FFBD3D");       // نارنجی/زرد
            this["InfoColor"] = Color.FromArgb("#3E7BFA");          // آبی روشن

            // حباب پیام‌ها
            this["SentMessageBubbleColor"] = Color.FromArgb("#EFECFF"); // بنفش بسیار روشن
            this["ReceivedMessageBubbleColor"] = Color.FromArgb("#F2F5FC"); // آبی بسیار روشن
            this["SentMessageTextColor"] = Color.FromArgb("#4A0ABF"); // بنفش تیره متنی
            this["ReceivedMessageTextColor"] = Color.FromArgb("#101C3D"); // سورمه‌ای تیره متنی

            // نشانگر وضعیت آنلاین/آفلاین
            this["OnlineStatusColor"] = Color.FromArgb("#00C48C");   // سبز روشن
            this["OfflineStatusColor"] = Color.FromArgb("#CACDD8");  // خاکستری روشن

            // رنگ های Splash و Loading
            this["SplashBackgroundColor"] = Color.FromArgb("#6200EA"); // بنفش عمیق و غنی (مطابق PrimaryColor)
            this["LoadingIndicatorColor"] = Color.FromArgb("#FF5E76"); // صورتی گرم (مطابق AccentColor)

            // رنگ های دکمه ها و المان های تعاملی
            this["ButtonHighlightColor"] = Color.FromArgb("#7D5BE5"); // بنفش روشن‌تر برای hover
            this["InputFieldBorderColor"] = Color.FromArgb("#DADCE7"); // خاکستری مایل به بنفش بسیار روشن
            this["InputFieldFocusBorderColor"] = Color.FromArgb("#B294FF"); // بنفش روشن برای فوکوس

            // رنگ های اضافی برای گرادیان
            this["GradientStart"] = Color.FromArgb("#6200EA"); // بنفش عمیق
            this["GradientEnd"] = Color.FromArgb("#9C42FF"); // بنفش روشن‌تر

            // سایه ها
            this["ElevationShadow1"] = new SolidColorBrush(Color.FromRgba(16, 28, 61, 0.08));
            this["ElevationShadow2"] = new SolidColorBrush(Color.FromRgba(16, 28, 61, 0.12));

            // متغیرهای اندازه و فاصله
            // این متغیرها را می‌توان در استایل‌های مختلف استفاده کرد تا طراحی یکنواخت باشد
            this["SpacingSmall"] = 4;
            this["SpacingMedium"] = 8;
            this["SpacingLarge"] = 16;
            this["SpacingXLarge"] = 24;

            this["CornerRadiusSmall"] = 4;
            this["CornerRadiusMedium"] = 8;
            this["CornerRadiusLarge"] = 16;
            this["CornerRadiusXLarge"] = 24;

            this["FontSizeSmall"] = 12;
            this["FontSizeMedium"] = 14;
            this["FontSizeLarge"] = 16;
            this["FontSizeXLarge"] = 20;
            this["FontSizeXXLarge"] = 24;

            // تنظیمات حباب پیام
            this["MessageBubblePadding"] = new Thickness(12, 10);
            this["MessageBubbleCornerRadius"] = new CornerRadius(18, 18, 18, 4);
            this["MessageSentBubbleCornerRadius"] = new CornerRadius(18, 18, 4, 18);
        }
    }
}
