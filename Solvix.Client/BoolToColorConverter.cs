using System.Globalization;

namespace Solvix.Client
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Valor predeterminado
            bool condition = false;

            // Intentar convertir el valor a bool
            if (value is bool boolValue)
            {
                condition = boolValue;
            }
            else if (value is string strValue)
            {
                bool.TryParse(strValue, out condition);
            }
            // Si el valor es null, tratar como false
            else if (value == null)
            {
                condition = false;
            }

            // Si la condición es true y hay un parámetro
            if (condition && parameter is string paramStr)
            {
                // Asignar colores según el parámetro
                return paramStr switch
                {
                    "Error" => Colors.Red,
                    "Success" => Colors.Green,
                    "Warning" => Colors.Orange,
                    "Info" => Colors.Blue,
                    _ => Colors.Gray
                };
            }

            // Color predeterminado
            try
            {
                if (Application.Current.Resources.TryGetValue("SecondaryTextColor", out var color))
                {
                    return color;
                }
            }
            catch { }

            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false;
        }
    }
}