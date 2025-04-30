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

            // مهم: از ShellContent به جای ContentPage استفاده کنید
            Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));

            Routing.RegisterRoute(nameof(NewChatPage), typeof(NewChatPage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));

            // اضافه کردن حالت مورد نیاز برای پشته ناوبری
            Shell.SetTabBarIsVisible(this, false);
            Shell.SetNavBarIsVisible(this, false);
        }
    }
}
