using Microsoft.Maui.Controls;
using System.Windows.Input;

namespace Solvix.Client.MVVM.Controls
{
    public partial class NeonButton : ContentView
    {
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(NeonButton), string.Empty);

        public static readonly BindableProperty TextColorProperty =
            BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(NeonButton), Colors.White);

        public static readonly BindableProperty FontSizeProperty =
            BindableProperty.Create(nameof(FontSize), typeof(double), typeof(NeonButton), 16.0);

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(NeonButton), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(NeonButton), null);

        public static readonly BindableProperty GlowColorProperty =
            BindableProperty.Create(nameof(GlowColor), typeof(Color), typeof(NeonButton), Colors.Cyan);

        public static readonly BindableProperty BorderColorProperty =
            BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(NeonButton), Colors.Cyan);

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public Color GlowColor
        {
            get => (Color)GetValue(GlowColorProperty);
            set => SetValue(GlowColorProperty, value);
        }

        public Color BorderColor
        {
            get => (Color)GetValue(BorderColorProperty);
            set => SetValue(BorderColorProperty, value);
        }

        public NeonButton()
        {
            InitializeComponent();
            BindingContext = this;
        }

        private async void OnTapped(object sender, EventArgs e)
        {
            // Glow animation
            _ = GlowEffect.FadeTo(0.5, 100);
            _ = MainButton.ScaleTo(0.95, 100);

            await Task.Delay(100);

            _ = GlowEffect.FadeTo(0, 200);
            _ = MainButton.ScaleTo(1, 200);

            if (Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }
        }
    }
}