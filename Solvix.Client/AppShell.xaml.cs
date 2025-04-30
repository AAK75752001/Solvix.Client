using Solvix.Client.MVVM.Views;

namespace Solvix.Client
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes for navigation
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));

            // Instead of registering ChatPage directly, we'll use a parameterized route format
            // This allows each chat ID to have a unique route path
            Routing.RegisterRoute($"{nameof(ChatPage)}", typeof(ChatPage));

            Routing.RegisterRoute(nameof(NewChatPage), typeof(NewChatPage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));

            // Shell settings
            Shell.SetTabBarIsVisible(this, false);
            Shell.SetNavBarIsVisible(this, false);
        }
    }
}