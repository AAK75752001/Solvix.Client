using Microsoft.Maui.Controls.Shapes;
using Solvix.Client.Core.Models;

namespace Solvix.Client.MVVM.Controls
{
    public class OnlineStatusIndicator : Grid
    {
        public static readonly BindableProperty UserProperty =
            BindableProperty.Create(nameof(User), typeof(UserModel), typeof(OnlineStatusIndicator), null, propertyChanged: OnUserPropertyChanged);

        public static readonly BindableProperty IsOnlineProperty =
            BindableProperty.Create(nameof(IsOnline), typeof(bool), typeof(OnlineStatusIndicator), false, propertyChanged: OnIsOnlinePropertyChanged);

        public static readonly BindableProperty SizeProperty =
            BindableProperty.Create(nameof(Size), typeof(double), typeof(OnlineStatusIndicator), 14.0, propertyChanged: OnSizePropertyChanged);

        public static readonly BindableProperty StrokeThicknessProperty =
            BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(OnlineStatusIndicator), 2.0, propertyChanged: OnStrokeThicknessPropertyChanged);

        public UserModel User
        {
            get => (UserModel)GetValue(UserProperty);
            set => SetValue(UserProperty, value);
        }

        public bool IsOnline
        {
            get => (bool)GetValue(IsOnlineProperty);
            set => SetValue(IsOnlineProperty, value);
        }

        public double Size
        {
            get => (double)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        private readonly Border _indicator;

        public OnlineStatusIndicator()
        {
            // تنظیم ویژگی‌های Grid
            VerticalOptions = LayoutOptions.Center;
            HorizontalOptions = LayoutOptions.Center;

            // ایجاد اندیکاتور وضعیت آنلاین
            _indicator = new Border
            {
                StrokeShape = new Ellipse(),
                BackgroundColor = Colors.Gray,
                Stroke = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            UpdateIndicatorSize();
            UpdateStrokeThickness();
            UpdateStatusColor();

            // اضافه کردن اندیکاتور به Grid
            Children.Add(_indicator);
        }

        private static void OnUserPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is OnlineStatusIndicator indicator && newValue is UserModel user)
            {
                indicator.IsOnline = user.IsOnline;
            }
        }

        private static void OnIsOnlinePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is OnlineStatusIndicator indicator)
            {
                indicator.UpdateStatusColor();
            }
        }

        private static void OnSizePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is OnlineStatusIndicator indicator)
            {
                indicator.UpdateIndicatorSize();
            }
        }

        private static void OnStrokeThicknessPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is OnlineStatusIndicator indicator)
            {
                indicator.UpdateStrokeThickness();
            }
        }

        private void UpdateIndicatorSize()
        {
            _indicator.WidthRequest = Size;
            _indicator.HeightRequest = Size;
        }

        private void UpdateStrokeThickness()
        {
            _indicator.StrokeThickness = StrokeThickness;
        }

        private void UpdateStatusColor()
        {
            Color onlineColor = Colors.Green;
            Color offlineColor = Colors.Gray;

            if (Application.Current?.Resources != null)
            {
                if (Application.Current.Resources.TryGetValue("OnlineStatusColor", out var onlineRes) && onlineRes is Color onlineResColor)
                {
                    onlineColor = onlineResColor;
                }

                if (Application.Current.Resources.TryGetValue("OfflineStatusColor", out var offlineRes) && offlineRes is Color offlineResColor)
                {
                    offlineColor = offlineResColor;
                }
            }

            _indicator.BackgroundColor = IsOnline ? onlineColor : offlineColor;
        }
    }
}