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

            // Instead of registering ChatPage directly, we'll use a parameterized route format
            // This allows each chat ID to have a unique route path
         

            // Shell settings
            Shell.SetTabBarIsVisible(this, false);
            Shell.SetNavBarIsVisible(this, false);
        }
    }
}