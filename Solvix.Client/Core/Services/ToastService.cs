using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Solvix.Client.Core.Interfaces;

namespace Solvix.Client.Core.Services
{
    public class ToastService : IToastService
    {
        public async Task ShowToastAsync(string message, ToastType type = ToastType.Info)
        {
            if (string.IsNullOrEmpty(message))
            {
                Console.WriteLine("Toast message is empty or null.");
                return;
            }

            var duration = GetDuration(type);
            var backgroundColor = GetBackgroundColor(type);
            var textColor = Colors.White;

            // Simple toast without using ToastOptions which may not be available in this version
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    var toast = Toast.Make(message, duration);
                    await toast.Show();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Toast error: {ex.Message}. Message was: {message}");
                }
            });
        }

        private ToastDuration GetDuration(ToastType type)
        {
            return type switch
            {
                ToastType.Success => ToastDuration.Short,
                ToastType.Warning => ToastDuration.Short,
                ToastType.Error => ToastDuration.Long,
                _ => ToastDuration.Short
            };
        }

        private Color GetBackgroundColor(ToastType type)
        {
            return type switch
            {
                ToastType.Success => Color.FromArgb("#2ecc71"), // Green
                ToastType.Warning => Color.FromArgb("#f39c12"), // Orange
                ToastType.Error => Color.FromArgb("#e74c3c"),   // Red
                _ => Color.FromArgb("#3498db")                  // Blue for Info
            };
        }
    }
}