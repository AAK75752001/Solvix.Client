using Microsoft.Maui.Graphics;

namespace Solvix.Client.Resources.Themes
{
    public partial class DarkThemeResources : ResourceDictionary
    {
        public DarkThemeResources()
        {
            // رنگ‌های پایه تم تیره
            this["PrimaryColor"] = Color.FromArgb("#BB86FC");        // بنفش روشن (برای کنتراست)
            this["SecondaryColor"] = Color.FromArgb("#03DAC6");      // Teal (مثل تم روشن)
            this["TertiaryColor"] = Color.FromArgb("#3700B3");       // بنفش استاندارد
            this["AccentColor"] = Color.FromArgb("#03DAC6");         // Teal

            this["PageBackgroundColor"] = Color.FromArgb("#121212");  // مشکی/خاکستری خیلی تیره
            this["CardBackgroundColor"] = Color.FromArgb("#1E1E1E");  // خاکستری تیره
            this["FrameBorderColor"] = Color.FromArgb("#272727");

            this["PrimaryTextColor"] = Color.FromArgb("#FFFFFF");     // سفید
            this["SecondaryTextColor"] = Color.FromArgb("#E0E0E0");   // سفید مایل به خاکستری
            this["TertiaryTextColor"] = Color.FromArgb("#BDBDBD");    // خاکستری روشن
            this["InverseTextColor"] = Color.FromArgb("#121212");     // مشکی

            this["SeparatorColor"] = Color.FromArgb("#333333");
            this["ShadowColor"] = Color.FromArgb("#BB000000");       // سایه تیره‌تر
            this["SuccessColor"] = Color.FromArgb("#81C784");        // سبز روشن
            this["ErrorColor"] = Color.FromArgb("#CF6679");          // قرمز/صورتی روشن
            this["WarningColor"] = Color.FromArgb("#FFB74D");        // نارنجی روشن
            this["InfoColor"] = Color.FromArgb("#64B5F6");           // آبی روشن

            // رنگ‌های حباب پیام
            this["SentMessageBubbleColorDark"] = Color.FromArgb("#3700B3"); // بنفش استاندارد
            this["ReceivedMessageBubbleColorDark"] = Color.FromArgb("#333333"); // خاکستری تیره‌تر
            this["SentMessageTextColorDark"] = Colors.White;
            this["ReceivedMessageTextColorDark"] = Color.FromArgb("#E0E0E0");

            // وضعیت آنلاین/آفلاین
            this["OnlineStatusColor"] = Color.FromArgb("#81C784");   // سبز روشن
            this["OfflineStatusColor"] = Color.FromArgb("#757575");  // خاکستری تیره‌تر
        }
    }
}