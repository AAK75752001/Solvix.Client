// Solvix.Client/Core/Services/ImprovedToastService.cs
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Layouts;
using Solvix.Client.Core.Interfaces;
using System.Collections.Concurrent;

namespace Solvix.Client.Core.Services
{
    public class ImprovedToastService : IToastService
    {
        private readonly ILogger<ImprovedToastService> _logger;
        private readonly ConcurrentQueue<ToastInfo> _pendingToasts = new ConcurrentQueue<ToastInfo>();
        private bool _isProcessing = false;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // Customizable toast appearance settings
        private static int ToastDuration => 3000; // milliseconds
        private static int ToastMargin => 20;
        private static double ToastOpacity => 0.9;
        private static double ToastCornerRadius => 10;
        private static int ToastWidth => 300;

        public ImprovedToastService(ILogger<ImprovedToastService> logger)
        {
            _logger = logger;
        }

        public async Task ShowToastAsync(string message, ToastType type = ToastType.Info)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("Toast message is empty or null - ignoring request");
                return;
            }

            _logger.LogDebug("Toast requested: {Message} (Type: {ToastType})", message, type);

            var toastInfo = new ToastInfo
            {
                Message = message,
                Type = type,
                CreatedAt = DateTime.UtcNow
            };

            _pendingToasts.Enqueue(toastInfo);

            // Start processing if not already running
            await ProcessPendingToastsAsync();
        }

        private async Task ProcessPendingToastsAsync()
        {
            // Use semaphore to ensure only one processing loop is active
            if (!await _semaphore.WaitAsync(0))
            {
                return; // Another process is already handling the queue
            }

            try
            {
                _isProcessing = true;

                while (_pendingToasts.TryDequeue(out var toastInfo))
                {
                    try
                    {
                        await DisplayToastAsync(toastInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error displaying toast: {Message}", toastInfo.Message);
                    }
                }
            }
            finally
            {
                _isProcessing = false;
                _semaphore.Release();
            }
        }

        private async Task DisplayToastAsync(ToastInfo toastInfo)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Get the current active page
                    var currentPage = GetCurrentPage();
                    if (currentPage == null)
                    {
                        _logger.LogWarning("Could not find current page to display toast");
                        return;
                    }

                    // Create the toast frame
                    var frame = new Frame
                    {
                        BackgroundColor = GetBackgroundColor(toastInfo.Type),
                        CornerRadius = (float)ToastCornerRadius,
                        Opacity = ToastOpacity,
                        Padding = new Thickness(15, 10),
                        Margin = new Thickness(ToastMargin),
                        WidthRequest = ToastWidth,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Start,
                        TranslationY = -100 // Start offscreen for animation
                    };

                    // Create the toast content
                    var label = new Label
                    {
                        Text = toastInfo.Message,
                        TextColor = Colors.White,
                        FontSize = 14,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center
                    };

                    frame.Content = label;

                    // Add the toast to the current page
                    AbsoluteLayout.SetLayoutBounds(frame, new Rect(0.5, 0, -1, -1));
                    AbsoluteLayout.SetLayoutFlags(frame, AbsoluteLayoutFlags.PositionProportional);

                    // Create or find an AbsoluteLayout for toasts
                    var toastLayout = EnsureToastLayoutExists(currentPage);
                    toastLayout.Children.Add(frame);

                    // Animate the toast in
                    await frame.TranslateTo(0, 0, 300, Easing.SpringOut);

                    // Wait for the toast duration
                    await Task.Delay(ToastDuration);

                    // Animate the toast out
                    await frame.TranslateTo(0, -100, 300, Easing.SpringIn);

                    // Remove the toast
                    toastLayout.Children.Remove(frame);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in toast UI operations");
                }
            });
        }

        private Page GetCurrentPage()
        {
            if (Application.Current?.MainPage == null)
                return null;

            var mainPage = Application.Current.MainPage;

            // Handle navigation page
            if (mainPage is NavigationPage navPage && navPage.CurrentPage != null)
                return navPage.CurrentPage;

            // Handle Shell
            if (mainPage is Shell shell && shell.CurrentPage != null)
                return shell.CurrentPage;

            // Handle TabbedPage
            if (mainPage is TabbedPage tabbedPage && tabbedPage.CurrentPage != null)
                return tabbedPage.CurrentPage;

            return mainPage;
        }

        private Color GetBackgroundColor(ToastType type)
        {
            return type switch
            {
                ToastType.Success => Color.FromArgb("#1E8E3E"), // Green
                ToastType.Warning => Color.FromArgb("#F9A825"), // Amber
                ToastType.Error => Color.FromArgb("#D93025"),   // Red
                _ => Color.FromArgb("#1A73E8")                  // Blue for Info
            };
        }

        private AbsoluteLayout EnsureToastLayoutExists(Page page)
        {
            // Look for existing toast layout
            var contentView = page.Content;

            // If content is already a Grid with our toast layout, use it
            if (contentView is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is AbsoluteLayout layout && layout.StyleId == "ToastLayout")
                    {
                        return layout;
                    }
                }

                // Create new toast layout and add to existing grid
                var toastLayout = new AbsoluteLayout
                {
                    StyleId = "ToastLayout",
                    InputTransparent = true // Let input events pass through
                };

                // Add as overlay
                Grid.SetRowSpan(toastLayout, grid.RowDefinitions.Count > 0 ? grid.RowDefinitions.Count : 1);
                Grid.SetColumnSpan(toastLayout, grid.ColumnDefinitions.Count > 0 ? grid.ColumnDefinitions.Count : 1);

                grid.Children.Add(toastLayout);
                return toastLayout;
            }

            // Otherwise, wrap the current content in a Grid with our toast layout
            var newGrid = new Grid();

            // Set the original content in the grid
            if (contentView != null)
            {
                newGrid.Children.Add(contentView);
            }

            // Add toast layout overlay
            var newToastLayout = new AbsoluteLayout
            {
                StyleId = "ToastLayout",
                InputTransparent = true
            };

            newGrid.Children.Add(newToastLayout);

            // Replace page content
            page.Content = newGrid;

            return newToastLayout;
        }

        private class ToastInfo
        {
            public string Message { get; set; }
            public ToastType Type { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}