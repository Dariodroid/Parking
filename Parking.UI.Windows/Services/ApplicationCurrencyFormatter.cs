using Parking.Application.Interfaces;

namespace Parking.UI.Windows.Services;

/// <summary>Adapta el formato monetario activo de la interfaz a los mensajes de aplicación.</summary>
public sealed class ApplicationCurrencyFormatter : ICurrencyFormatter
{
    /// <summary>Usa el símbolo actual sin modificar el importe.</summary>
    /// <param name="amount">Valor decimal exacto.</param>
    /// <returns>Texto con el símbolo configurado.</returns>
    public string Format(decimal amount) => CurrencyDisplay.Format(amount);
}
