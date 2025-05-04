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
                .UseMauiCommunityToolkit() // Properly initialize CommunityToolkit
                .ConfigureFonts(fonts =>
                {
                    // Register system fonts as fallbacks first
                    fonts.AddFont("Segoe UI", "SegoeUI");

                    // Try to add the app's custom fonts
                    try
                    {
                        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading OpenSans fonts: {ex.Message}");
                        // App will fall back to system fonts
                    }

                    // Material Icons font - critical for UI 
                    try
                    {
                        fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading MaterialIcons font: {ex.Message}");
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

            // API-related services
            services.AddSingleton<IApiService, ApiService>();
            services.AddSingleton<IAuthService, AuthService>();

            // Domain services
            services.AddSingleton<IUserService, UserService>();
        }

        private static void RegisterViewModels(IServiceCollection services)
        {
            // Auth view models
            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();

            // Main view models
        
        }

        private static void RegisterViews(IServiceCollection services)
        {
            // Auth views
            services.AddTransient<LoginPage>();
            services.AddTransient<RegisterPage>();

            // Main views
           
        }
    }
}