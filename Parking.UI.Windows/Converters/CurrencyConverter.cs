using Parking.UI.Windows.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Parking.UI.Windows.Converters;

/// <summary>Presenta importes con el signo configurado y deja intacto el valor decimal enlazado.</summary>
public sealed class CurrencyConverter : IValueConverter
{
    /// <summary>Transforma un valor decimal en texto monetario para WPF.</summary>
    /// <param name="value">Importe decimal o nulo.</param>
    /// <param name="targetType">Tipo solicitado por el control.</param>
    /// <param name="parameter">Sufijo opcional, por ejemplo /h.</param>
    /// <param name="culture">Cultura del enlace, que no cambia el signo elegido.</param>
    /// <returns>Importe formateado con el sufijo indicado.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is decimal amount
            ? (parameter as string is { } text && text.EndsWith(": ", StringComparison.Ordinal)
                ? text + CurrencyDisplay.Format(amount)
                : CurrencyDisplay.Format(amount) + (parameter as string ?? string.Empty))
            : string.Empty;

    /// <summary>Los textos monetarios de solo lectura no se convierten de vuelta a la base.</summary>
    /// <param name="value">Texto del control.</param>
    /// <param name="targetType">Tipo de la propiedad origen.</param>
    /// <param name="parameter">Parámetro opcional.</param>
    /// <param name="culture">Cultura del enlace.</param>
    /// <returns>La marca de enlace sin actualización.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}
