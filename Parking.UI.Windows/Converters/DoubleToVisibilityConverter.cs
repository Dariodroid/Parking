using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Parking.UI.Windows.Converters;

/// <summary>Muestra las etiquetas del menú cuando hay ancho suficiente.</summary>
public class DoubleToVisibilityConverter : IValueConverter
{
    /// <summary>Convierte el ancho disponible en visibilidad del texto.</summary>
    /// <param name="value">Ancho numérico observado por el enlace.</param>
    /// <param name="targetType">Tipo de visibilidad solicitado por WPF.</param>
    /// <param name="parameter">Parámetro opcional del enlace, no utilizado.</param>
    /// <param name="culture">Cultura del enlace, no utilizada.</param>
    /// <returns>Visible si el ancho supera 105; Collapsed en otro caso.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // El umbral deja visibles las etiquetas en los anchos ampliados.
        if (value is double d && d > 105)
            return Visibility.Visible;
        // En el menú compacto solo quedan los iconos.
        return Visibility.Collapsed;
    }

    /// <summary>La visibilidad no determina un ancho exacto, por lo que no hay conversión inversa.</summary>
    /// <param name="value">Visibilidad recibida de WPF.</param>
    /// <param name="targetType">Tipo del origen solicitado por WPF.</param>
    /// <param name="parameter">Parámetro opcional del enlace.</param>
    /// <param name="culture">Cultura del enlace.</param>
    /// <returns>No retorna: esta dirección no está implementada.</returns>
    /// <exception cref="NotImplementedException">Siempre, porque el enlace se usa solo de origen a destino.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Una misma visibilidad puede corresponder a numerosos anchos.
        throw new NotImplementedException();
    }
}
