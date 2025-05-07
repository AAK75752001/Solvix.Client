using Solvix.Client.MVVM.Views;

namespace Solvix.Client
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(ChatListPage), typeof(ChatListPage));
            Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(NewChatPage), typeof(NewChatPage));



            CurrentItem = new ShellContent
            {
                Title = "سالویکس",
                ContentTemplate = new DataTemplate(typeof(ChatListPage)),
                Route = nameof(ChatListPage)
            };

            Shell.SetNavBarIsVisible(this, true);
        }
    }
}