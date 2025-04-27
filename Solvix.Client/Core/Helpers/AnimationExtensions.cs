using Solvix.Client.Core;

namespace Solvix.Client.Helpers
{
    public static class AnimationExtensions
    {
        // Fade In animation
        public static async Task FadeInAsync(this VisualElement element, uint duration = Constants.AnimationDurations.Medium, Easing easing = null)
        {
            element.Opacity = 0;
            element.IsVisible = true;

            await element.FadeTo(1, duration, easing ?? Easing.CubicOut);
        }

        // Fade Out animation
        public static async Task FadeOutAsync(this VisualElement element, uint duration = Constants.AnimationDurations.Medium, Easing easing = null)
        {
            await element.FadeTo(0, duration, easing ?? Easing.CubicIn);
            element.IsVisible = false;
        }

        // Scale In animation
        public static async Task ScaleInAsync(this VisualElement element, double scale = 1.0, uint duration = Constants.AnimationDurations.Medium, Easing easing = null)
        {
            element.Scale = 0.8;
            element.Opacity = 0;
            element.IsVisible = true;

            await Task.WhenAll(
                element.ScaleTo(scale, duration, easing ?? Easing.SpringOut),
                element.FadeTo(1, duration, easing ?? Easing.CubicOut)
            );
        }

        // Scale Out animation
        public static async Task ScaleOutAsync(this VisualElement element, uint duration = Constants.AnimationDurations.Medium, Easing easing = null)
        {
            await Task.WhenAll(
                element.ScaleTo(0.8, duration, easing ?? Easing.CubicIn),
                element.FadeTo(0, duration, easing ?? Easing.CubicIn)
            );

            element.IsVisible = false;
        }

        // Slide In from right
        public static async Task SlideInFromRightAsync(this View element, double distance = 100, uint duration = Constants.AnimationDurations.Medium, Easing easing = null)
        {
            element.TranslationX = distance;
            element.Opacity = 0;
            element.IsVisible = true;

            await Task.WhenAll(
                element.TranslateTo(0, 0, duration, easing ?? Easing.CubicOut),
                element.FadeTo(1, duration, easing ?? Easing.CubicOut)
            );
        }

        // Slide Out to right
        public static async Task SlideOutToRightAsync(this View element, double distance = 100, uint duration = Constants.AnimationDurations.Medium, Easing easing = null)
        {
            await Task.WhenAll(
                element.TranslateTo(distance, 0, duration, easing ?? Easing.CubicIn),
                element.FadeTo(0, duration, easing ?? Easing.CubicIn)
            );

            element.IsVisible = false;
        }

        // Slide In from left
        public static async Task SlideInFromLeftAsync(this View element, double distance = 100, uint duration = Constants.AnimationDurations.Medium, Easing easing = null)
        {
            element.TranslationX = -distance;
            element.Opacity = 0;
            element.IsVisible = true;

            await Task.WhenAll(
                element.TranslateTo(0, 0, duration, easing ?? Easing.CubicOut),
                element.FadeTo(1, duration, easing ?? Easing.CubicOut)
            );
        }

        // Slide Out to left
        public static async Task SlideOutToLeftAsync(this View element, double distance = 100, uint duration = Constants.AnimationDurations.Medium, Easing easing = null)
        {
            await Task.WhenAll(
                element.TranslateTo(-distance, 0, duration, easing ?? Easing.CubicIn),
                element.FadeTo(0, duration, easing ?? Easing.CubicIn)
            );

            element.IsVisible = false;
        }

        // Pulse animation
        public static async Task PulseAsync(this VisualElement element, uint duration = Constants.AnimationDurations.Medium, double scale = 1.1)
        {
            await element.ScaleTo(scale, duration / 2, Easing.SinOut);
            await element.ScaleTo(1, duration / 2, Easing.SinIn);
        }

        // Shake animation
        public static async Task ShakeAsync(this VisualElement element, uint duration = Constants.AnimationDurations.Medium, double distance = 10)
        {
            uint durationPerShake = duration / 6;

            await element.TranslateTo(-distance, 0, durationPerShake);
            await element.TranslateTo(distance, 0, durationPerShake);
            await element.TranslateTo(-distance / 2, 0, durationPerShake);
            await element.TranslateTo(distance / 2, 0, durationPerShake);
            await element.TranslateTo(-distance / 4, 0, durationPerShake);
            await element.TranslateTo(0, 0, durationPerShake);
        }
    }
}
