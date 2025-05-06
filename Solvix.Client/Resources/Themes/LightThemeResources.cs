using Microsoft.Maui.Graphics;

namespace Solvix.Client.Resources.Themes
{
    public class LightThemeResources : ResourceDictionary
    {
        public LightThemeResources()
        {
            // App Theme Colors - Modern light theme with gradients
            this["PrimaryColor"] = Color.FromArgb("#8A2BE2");        // Vibrant Purple (same as dark)
            this["SecondaryColor"] = Color.FromArgb("#E0E0FF");      // Light lavender
            this["TertiaryColor"] = Color.FromArgb("#FF9EFF");       // Light magenta
            this["AccentColor"] = Color.FromArgb("#00C07F");         // Green

            // Page and Controls
            this["PageBackgroundColor"] = Color.FromArgb("#F6F6F6");  // Near white
            this["CardBackgroundColor"] = Colors.White;
            this["FrameBorderColor"] = Color.FromArgb("#E0E0E0");     // Light gray border

            // Text Colors
            this["PrimaryTextColor"] = Color.FromArgb("#151515");     // Near black
            this["SecondaryTextColor"] = Color.FromArgb("#555555");   // Dark gray
            this["TertiaryTextColor"] = Color.FromArgb("#888888");    // Medium gray
            this["InverseTextColor"] = Colors.White;

            // Other UI Elements
            this["SeparatorColor"] = Color.FromArgb("#EEEEEE");      // Separator color
            this["ShadowColor"] = Color.FromArgb("#22000000");       // Light shadow
            this["SuccessColor"] = Color.FromArgb("#00C07F");        // Green
            this["ErrorColor"] = Color.FromArgb("#E53935");          // Red
            this["WarningColor"] = Color.FromArgb("#FFC107");        // Amber
            this["InfoColor"] = Color.FromArgb("#2196F3");           // Blue

            // Message Bubbles - Modern with subtle gradients
            this["SentMessageBubbleColor"] = Color.FromArgb("#E1D2FF"); // Light purple
            this["ReceivedMessageBubbleColor"] = Colors.White;
            this["SentMessageTextColor"] = Color.FromArgb("#151515");
            this["ReceivedMessageTextColor"] = Color.FromArgb("#151515");

            // Status colors
            this["OnlineStatusColor"] = Color.FromArgb("#00C07F");   // Green
            this["OfflineStatusColor"] = Color.FromArgb("#BDBDBD");  // Gray

            // Brand colors - for gradients and accents
            this["GradientStart"] = Color.FromArgb("#8A2BE2");       // Vibrant Purple
            this["GradientMiddle"] = Color.FromArgb("#FF9EFF");      // Light magenta
            this["GradientEnd"] = Color.FromArgb("#E0E0FF");         // Light lavender
        }
    }
}