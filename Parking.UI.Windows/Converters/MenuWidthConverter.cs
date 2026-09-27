using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Parking.UI.Windows.Converters;

/// <summary>Calcula el ancho del menú según su estado y los paneles desplegados.</summary>
public class MenuWidthConverter : IMultiValueConverter
{
    /// <summary>Selecciona un ancho de menú amplio cuando está abierto o contiene un panel expandido.</summary>
    /// <param name="values">Estados en orden: menú, Reportes y Configuración.</param>
    /// <param name="targetType">Tipo de destino suministrado por WPF.</param>
    /// <param name="parameter">Parámetro opcional del enlace, no utilizado.</param>
    /// <param name="culture">Cultura del enlace, no utilizada para este cálculo.</param>
    /// <returns>Ancho de 240 cuando el menú o un panel está expandido; 100 en otro caso.</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // La posición de cada valor coincide con el orden del MultiBinding de la vista.
        bool menuExpanded = values.Length > 0 && values[0] is bool b1 && b1;
        bool exp1Expanded = values.Length > 1 && values[1] is bool b2 && b2;
        bool exp2Expanded = values.Length > 2 && values[2] is bool b3 && b3;

        // El menú abierto tiene prioridad; los paneles desplegados también necesitan ancho completo.
        if (menuExpanded) return new GridLength(240);
        if (exp1Expanded || exp2Expanded) return new GridLength(240);
        // El menú cerrado sin paneles usa el ancho reducido.
        return new GridLength(100);
    }

    /// <summary>La conversión inversa no está definida para un ancho derivado de varios estados.</summary>
    /// <param name="value">Ancho recibido de WPF.</param>
    /// <param name="targetTypes">Tipos de los valores de origen.</param>
    /// <param name="parameter">Parámetro opcional del enlace.</param>
    /// <param name="culture">Cultura del enlace.</param>
    /// <returns>No retorna: esta dirección no está implementada.</returns>
    /// <exception cref="NotImplementedException">Siempre, porque el enlace se usa solo de origen a destino.</exception>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        // Un ancho no permite reconstruir de forma única los tres estados originales.
        throw new NotImplementedException();
    }
}
