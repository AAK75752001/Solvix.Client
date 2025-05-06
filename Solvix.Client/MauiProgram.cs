using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Solvix.Client.Core.Interfaces;
using Solvix.Client.Core.Services;
using Solvix.Client.MVVM.ViewModels;
using Solvix.Client.MVVM.Views;

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
                    fonts.AddFont("Segoe UI", "SegoeUI");

                    try
                    {
                        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                        fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                        fonts.AddFont("Vazir.ttf", "Vazirmatn");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading OpenSans fonts: {ex.Message}");
                    }
                });

            // Add logging
#if DEBUG
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
            builder.Logging.SetMinimumLevel(LogLevel.Information);
#endif

            // Register services
            RegisterServices(builder.Services);

            // Register view models
            RegisterViewModels(builder.Services);

            // Register views
            RegisterViews(builder.Services);

            return builder.Build();
        }

        private static void RegisterServices(IServiceCollection services)
        {
            // Core services
            services.AddSingleton<ISecureStorageService, SecureStorageService>();
            services.AddSingleton<IConnectivityService, ConnectivityService>();
            services.AddSingleton<IToastService, ImprovedToastService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddScoped<IChatService, ChatService>();

            // Authentication & Security
            services.AddSingleton<ITokenManager, TokenManager>();
            services.AddSingleton<IAuthService, AuthService>();

            // Real-time communication
            services.AddSingleton<ISignalRService, SignalRService>();

            // API-related services
            services.AddSingleton<IApiService, ApiService>();

            // Domain services
            services.AddSingleton<IUserService, UserService>();
        }

        private static void RegisterViewModels(IServiceCollection services)
        {
            // Auth view models
            services.AddTransient<LoginViewModel>();
            services.AddTransient<ChatListViewModel>();
            services.AddTransient<ChatPageViewModel>();

            // Main view models

        }

        private static void RegisterViews(IServiceCollection services)
        {
            // Auth views
            services.AddTransient<LoginPage>();
            services.AddTransient<ChatListPage>();
            services.AddTransient<ChatPage>();

            // Main views

        }
    }
}