using Microsoft.Maui.Graphics;

namespace Solvix.Client.Resources.Themes
{
    public partial class LightThemeResources : ResourceDictionary
    {
        public LightThemeResources()
        {

            // رنگ‌های پایه تم روشن
            this["PrimaryColor"] = Color.FromArgb("#6200EE");        // بنفش استاندارد متریال
            this["SecondaryColor"] = Color.FromArgb("#03DAC6");      // Teal استاندارد متریال
            this["TertiaryColor"] = Color.FromArgb("#3700B3");       // بنفش تیره‌تر
            this["AccentColor"] = Color.FromArgb("#018786");         // Teal تیره‌تر

            this["PageBackgroundColor"] = Color.FromArgb("#FFFFFF");  // سفید
            this["CardBackgroundColor"] = Color.FromArgb("#F5F5F5");  // خاکستری خیلی روشن
            this["FrameBorderColor"] = Color.FromArgb("#E0E0E0");

            this["PrimaryTextColor"] = Color.FromArgb("#000000");     // مشکی
            this["SecondaryTextColor"] = Color.FromArgb("#555555");   // خاکستری تیره
            this["TertiaryTextColor"] = Color.FromArgb("#888888");    // خاکستری متوسط
            this["InverseTextColor"] = Colors.White;                // سفید

            this["SeparatorColor"] = Color.FromArgb("#EEEEEE");
            this["ShadowColor"] = Color.FromArgb("#22000000");
            this["SuccessColor"] = Color.FromArgb("#4CAF50");        // سبز
            this["ErrorColor"] = Color.FromArgb("#B00020");          // قرمز تیره متریال
            this["WarningColor"] = Color.FromArgb("#FB8C00");        // نارنجی
            this["InfoColor"] = Color.FromArgb("#1976D2");           // آبی

            // رنگ‌های حباب پیام
            this["SentMessageBubbleColorLight"] = Color.FromArgb("#E1F5FE"); // آبی خیلی روشن
            this["ReceivedMessageBubbleColorLight"] = Color.FromArgb("#F1F1F1"); // خاکستری روشن
            this["SentMessageTextColorLight"] = Color.FromArgb("#0D47A1"); // آبی تیره
            this["ReceivedMessageTextColorLight"] = Color.FromArgb("#151515"); // مشکی

            // وضعیت آنلاین/آفلاین
            this["OnlineStatusColor"] = Color.FromArgb("#4CAF50");   // سبز
            this["OfflineStatusColor"] = Color.FromArgb("#BDBDBD");  // خاکستری
        }
    }
}