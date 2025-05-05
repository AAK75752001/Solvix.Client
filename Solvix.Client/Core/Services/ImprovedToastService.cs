using Microsoft.Extensions.Logging;
using Microsoft.Maui.Layouts;
using Solvix.Client.Core.Interfaces;
using System.Collections.Concurrent;
using Colors = Microsoft.Maui.Graphics.Colors;
using Microsoft.Maui.Controls.Shapes;
using System.Linq;

namespace Solvix.Client.Core.Services
{
    public class ImprovedToastService : IToastService
    {
        private readonly ILogger<ImprovedToastService> _logger;
        private static readonly ConcurrentQueue<ToastInfo> _pendingToasts = new ConcurrentQueue<ToastInfo>();
        private static bool _isProcessing = false;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private static int ToastDuration => 3000;
        private static int ToastMargin => 20;
        private static double ToastOpacity => 0.95;
        private static double ToastCornerRadius => 25;
        private static int MaxToastWidth => 350;
        private static double ToastFontSize => 14;


        public ImprovedToastService(ILogger<ImprovedToastService> logger)
        {
            _logger = logger;
        }

        public async Task ShowToastAsync(string message, ToastType type = ToastType.Info)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("پیام Toast خالی است - درخواست نادیده گرفته شد");
                return;
            }
            _logger.LogDebug("درخواست Toast: {Message} (نوع: {ToastType})", message, type);
            var toastInfo = new ToastInfo { Message = message, Type = type };
            _pendingToasts.Enqueue(toastInfo);
            await ProcessPendingToastsAsync();
        }

        private async Task ProcessPendingToastsAsync()
        {
            if (!await _semaphore.WaitAsync(0)) return;
            try
            {
                _isProcessing = true;
                while (_pendingToasts.TryDequeue(out var toastInfo))
                {
                    try { await DisplayToastAsync(toastInfo); }
                    catch (Exception ex) { _logger.LogError(ex, "خطا در نمایش Toast: {Message}", toastInfo.Message); }
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
                    var currentPage = GetCurrentPage();
                    if (currentPage is not ContentPage contentPage || contentPage.Content == null)
                    {
                        _logger.LogWarning("صفحه فعالی (ContentPage) برای نمایش Toast پیدا نشد");
                        return;
                    }

                    var toastBorder = new Border
                    {
                        BackgroundColor = GetBackgroundColor(toastInfo.Type),
                        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(ToastCornerRadius) },
                        Padding = new Thickness(16, 10),
                        Margin = new Thickness(ToastMargin),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Start,
                        Opacity = 0,
                        TranslationY = -50,
                        MaximumWidthRequest = MaxToastWidth,
                        Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.3f, Radius = 8, Offset = new Point(4, 4) }
                    };

                    var contentLayout = new StackLayout
                    {
                        Orientation = StackOrientation.Horizontal,
                        Spacing = 10,
                        VerticalOptions = LayoutOptions.Center
                    };
                    contentLayout.Children.Add(new Label
                    {
                        Text = GetIcon(toastInfo.Type),
                        TextColor = Colors.White,
                        FontFamily = "MaterialIcons",
                        FontSize = 18,
                        VerticalTextAlignment = TextAlignment.Center
                    });
                    contentLayout.Children.Add(new Label
                    {
                        Text = toastInfo.Message,
                        TextColor = Colors.White,
                        FontSize = ToastFontSize,
                        HorizontalOptions = LayoutOptions.StartAndExpand,
                        VerticalTextAlignment = TextAlignment.Center,
                        LineBreakMode = LineBreakMode.WordWrap
                    });
                    toastBorder.Content = contentLayout;

                    var overlay = EnsureOverlayExists(contentPage);
                    AbsoluteLayout.SetLayoutFlags(toastBorder, AbsoluteLayoutFlags.PositionProportional);
                    AbsoluteLayout.SetLayoutBounds(toastBorder, new Rect(0.5, 0, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
                    overlay.Add(toastBorder);

                    await Task.WhenAll(
                        toastBorder.FadeTo(ToastOpacity, 250, Easing.CubicOut),
                        toastBorder.TranslateTo(0, ToastMargin, 250, Easing.CubicOut)
                    );
                    await Task.Delay(ToastDuration);
                    await Task.WhenAll(
                        toastBorder.FadeTo(0, 250, Easing.CubicIn),
                        toastBorder.TranslateTo(0, -50, 250, Easing.CubicIn)
                    );

                    overlay.Remove(toastBorder);
                    if (!overlay.Children.Any())
                    {
                        RemoveOverlay(contentPage, overlay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در عملیات UI مربوط به Toast");
                }
            });
        }

        // --- متدهای کمکی ---
        private Page? GetCurrentPage()
        {
            var mainPage = Application.Current?.MainPage;
            if (mainPage == null) return null;
            if (mainPage is NavigationPage navPage) return navPage.CurrentPage;
            if (mainPage is Shell shell) return shell.CurrentPage;
            if (mainPage is TabbedPage tabbedPage) return tabbedPage.CurrentPage;
            return mainPage;
        }

        private Color GetBackgroundColor(ToastType type) => type switch
        {
            ToastType.Success => Color.FromArgb("#1E8E3E"),
            ToastType.Warning => Color.FromArgb("#F9A825"),
            ToastType.Error => Color.FromArgb("#D93025"),
            _ => Color.FromArgb("#1A73E8")
        };
        private string GetIcon(ToastType type) => type switch
        {
            ToastType.Success => "check_circle",
            ToastType.Warning => "warning",
            ToastType.Error => "error",
            _ => "info"
        };
        private const string OverlayStyleId = "ToastOverlayAbsoluteLayout";

        private AbsoluteLayout EnsureOverlayExists(ContentPage page)
        {
            if (page.Content is Grid baseGrid && baseGrid.Children.OfType<AbsoluteLayout>().FirstOrDefault(al => al.StyleId == OverlayStyleId) is AbsoluteLayout existingOverlay)
            {
                baseGrid.Children.Remove(existingOverlay);
                baseGrid.Children.Add(existingOverlay);
                return existingOverlay;
            }

            var overlay = new AbsoluteLayout { StyleId = OverlayStyleId, InputTransparent = true };

            if (page.Content is Grid currentGrid)
            {
                Grid.SetRowSpan(overlay, currentGrid.RowDefinitions.Count > 0 ? currentGrid.RowDefinitions.Count : 1);
                Grid.SetColumnSpan(overlay, currentGrid.ColumnDefinitions.Count > 0 ? currentGrid.ColumnDefinitions.Count : 1);
                currentGrid.Children.Add(overlay);
            }
            else
            {
                var originalContent = page.Content;
                var newGrid = new Grid();
                if (originalContent != null) { newGrid.Children.Add(originalContent); }
                newGrid.Children.Add(overlay);
                page.Content = newGrid;
            }
            return overlay;
        }

        private void RemoveOverlay(ContentPage page, AbsoluteLayout overlay)
        {
            if (overlay.Parent is Grid parentGrid)
            {
                try
                {
                    parentGrid.Children.Remove(overlay);
                    _logger.LogDebug("Toast overlay removed from parent Grid.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing toast overlay from its parent Grid.");
                }
            }
            else if (page.Content == overlay)
            {
                page.Content = null;
                _logger.LogWarning("Toast overlay was the direct content of the page and is now removed.");
            }
            else
            {
                _logger.LogWarning("Could not find the parent Grid for the toast overlay to remove it.");
            }
        }
       

        private class ToastInfo
        {
            public string Message { get; set; } = string.Empty;
            public ToastType Type { get; set; }
        }
    }
}