using Microsoft.Maui.Controls;

namespace Solvix.Client.Core.Effects
{
    public static class GlowingEffect
    {
        public static readonly BindableProperty HasGlowProperty =
            BindableProperty.CreateAttached("HasGlow", typeof(bool), typeof(GlowingEffect), false,
                propertyChanged: OnHasGlowChanged);

        public static readonly BindableProperty GlowColorProperty =
            BindableProperty.CreateAttached("GlowColor", typeof(Color), typeof(GlowingEffect), Colors.Cyan);

        public static bool GetHasGlow(BindableObject view)
        {
            return (bool)view.GetValue(HasGlowProperty);
        }

        public static void SetHasGlow(BindableObject view, bool value)
        {
            view.SetValue(HasGlowProperty, value);
        }

        public static Color GetGlowColor(BindableObject view)
        {
            return (Color)view.GetValue(GlowColorProperty);
        }

        public static void SetGlowColor(BindableObject view, Color value)
        {
            view.SetValue(GlowColorProperty, value);
        }

        private static void OnHasGlowChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is not View view) return;

            if ((bool)newValue)
            {
                var glowColor = GetGlowColor(bindable);
                ApplyGlow(view, glowColor);
            }
            else
            {
                RemoveGlow(view);
            }
        }

        private static void ApplyGlow(View view, Color glowColor)
        {
            view.Shadow = new Shadow
            {
                Brush = new SolidColorBrush(glowColor),
                Offset = new Point(0, 0),
                Radius = 15,
                Opacity = 0.7f
            };
        }

        private static void RemoveGlow(View view)
        {
            view.Shadow = null;
        }
    }
}