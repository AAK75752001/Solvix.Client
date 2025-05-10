using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Services;
using Solvix.Client.MVVM.ViewModels;
using Solvix.Client.MVVM.Views;
using Solvix.Client.Core.Converters;

namespace Solvix.Client
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                    fonts.AddFont("Vazir.ttf", "Vazirmatn");
                });

#if DEBUG
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

            // Register all services
            builder.Services.AddSingleton<ISecureStorageService, SecureStorageService>();
            builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
            builder.Services.AddSingleton<IToastService, ImprovedToastService>();
            builder.Services.AddSingleton<IThemeService, ThemeService>();
            builder.Services.AddSingleton<ITokenManager, TokenManager>();
            builder.Services.AddSingleton<IAuthService, AuthService>();
            builder.Services.AddSingleton<ISignalRService, SignalRService>();
            builder.Services.AddSingleton<IApiService, ApiService>();
            builder.Services.AddSingleton<IChatService, ChatService>();
            builder.Services.AddSingleton<IUserService, UserService>();
            builder.Services.AddSingleton<INavigationService, NavigationService>();

            // Register ViewModels
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<ChatListViewModel>();
            builder.Services.AddTransient<ChatPageViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<NewChatViewModel>();

            // Register Views
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<ChatListPage>();
            builder.Services.AddTransient<ChatPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<NewChatPage>();

            var app = builder.Build();

            // Initialize theme service after build
            var themeService = app.Services.GetService<IThemeService>();
            themeService?.LoadSavedTheme();

            return app;
        }
    }
}