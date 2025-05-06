using Microsoft.Maui.Graphics;

namespace Solvix.Client.Resources.Themes
{
    public class DarkThemeResources : ResourceDictionary
    {
        public DarkThemeResources()
        {
            // App Theme Colors - Neon dark theme with gradients
            this["PrimaryColor"] = Color.FromArgb("#8A2BE2");        // Vibrant Purple
            this["SecondaryColor"] = Color.FromArgb("#00FFFF");      // Cyan 
            this["TertiaryColor"] = Color.FromArgb("#FF00FF");       // Magenta
            this["AccentColor"] = Color.FromArgb("#00FF7F");         // Spring Green

            // Page and Controls
            this["PageBackgroundColor"] = Color.FromArgb("#121212");  // Near black
            this["CardBackgroundColor"] = Color.FromArgb("#1E1E1E");  // Dark gray
            this["FrameBorderColor"] = Color.FromArgb("#272727");     // Medium gray border

            // Text Colors
            this["PrimaryTextColor"] = Colors.White;
            this["SecondaryTextColor"] = Color.FromArgb("#CCCCCC");   // Light gray
            this["TertiaryTextColor"] = Color.FromArgb("#999999");    // Medium gray
            this["InverseTextColor"] = Color.FromArgb("#222222");     // Near black

            // Other UI Elements
            this["SeparatorColor"] = Color.FromArgb("#333333");      // Separator color
            this["ShadowColor"] = Color.FromArgb("#88000000");       // Semi-transparent black
            this["SuccessColor"] = Color.FromArgb("#00FF7F");        // Spring Green
            this["ErrorColor"] = Color.FromArgb("#FF3050");          // Neon Red
            this["WarningColor"] = Color.FromArgb("#FFFF00");        // Neon Yellow
            this["InfoColor"] = Color.FromArgb("#00FFFF");           // Cyan

            // Message Bubbles - Neon styles with gradients
            this["SentMessageBubbleColor"] = Color.FromArgb("#6A1B9A");   // Dark purple
            this["ReceivedMessageBubbleColor"] = Color.FromArgb("#242424");
            this["SentMessageTextColor"] = Colors.White;
            this["ReceivedMessageTextColor"] = Colors.White;

            // Status colors
            this["OnlineStatusColor"] = Color.FromArgb("#00FF7F");   // Spring Green
            this["OfflineStatusColor"] = Color.FromArgb("#757575");  // Gray

            // Brand colors - for gradients and accents
            this["GradientStart"] = Color.FromArgb("#8A2BE2");       // Vibrant Purple
            this["GradientMiddle"] = Color.FromArgb("#FF00FF");      // Magenta
            this["GradientEnd"] = Color.FromArgb("#00FFFF");         // Cyan
        }
    }
}