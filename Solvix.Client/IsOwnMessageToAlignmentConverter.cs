using System.Globalization;

namespace Solvix.Client
{
    public class IsOwnMessageToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOwnMessage = false;

            // Conversión segura del valor
            if (value is bool boolValue)
            {
                isOwnMessage = boolValue;
            }

            // Para conversión a LayoutOptions
            if (targetType == typeof(LayoutOptions))
            {
                return isOwnMessage ? LayoutOptions.End : LayoutOptions.Start;
            }

            // Para conversión a Color
            if (typeof(Color).IsAssignableFrom(targetType))
            {
                // Si hay un parámetro, lo procesamos
                if (parameter is string paramStr)
                {
                    if (paramStr == "BgColor")
                    {
                        try
                        {
                            // Intentar obtener colores de Resources
                            if (isOwnMessage)
                            {
                                if (Application.Current.Resources.TryGetValue("SentMessageBubbleColor", out var sentColor))
                                    return sentColor;
                                return Colors.LightBlue;
                            }
                            else
                            {
                                if (Application.Current.Resources.TryGetValue("ReceivedMessageBubbleColor", out var receivedColor))
                                    return receivedColor;
                                return Colors.LightGray;
                            }
                        }
                        catch
                        {
                            // Colores predeterminados en caso de error
                            return isOwnMessage ? Colors.LightBlue : Colors.LightGray;
                        }
                    }
                    else if (paramStr == "TextColor")
                    {
                        try
                        {
                            // Intentar obtener colores de Resources
                            if (isOwnMessage)
                            {
                                if (Application.Current.Resources.TryGetValue("SentMessageTextColor", out var sentTextColor))
                                    return sentTextColor;
                                return Colors.Black;
                            }

                            else
                            {
                                if (Application.Current.Resources.TryGetValue("ReceivedMessageTextColor", out var receivedTextColor))
                                    return receivedTextColor;
                                return Colors.Black;
                            }
                        }
                        catch
                        {
                            // Color predeterminado para texto
                            return Colors.Black;
                        }
                    }
                }

                // Valor por defecto para Color
                return Colors.Gray;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false;
        }
    }
}