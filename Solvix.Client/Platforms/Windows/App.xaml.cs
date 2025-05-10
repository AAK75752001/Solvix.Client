using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace Solvix.Client.WinUI
{
    public partial class App : MauiWinUIApplication
    {
        const int WindowWidth = 400;
        const int WindowHeight = 800;

        public App()
        {
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

            var currentWindow = Application.Windows[0].Handler?.PlatformView;
            if (currentWindow != null)
            {
                var windowHandle = WindowNative.GetWindowHandle(currentWindow);
                var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = false;
                    presenter.IsMaximizable = false;
                }
            }
        }
    }
}