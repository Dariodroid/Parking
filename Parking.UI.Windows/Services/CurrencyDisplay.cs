using System.Globalization;

namespace Parking.UI.Windows.Services;

/// <summary>Da a todos los importes el mismo símbolo visual sin modificar su valor decimal.</summary>
public static class CurrencyDisplay
{
    private static CultureInfo _culture = CreateCulture("$");
    private static string _symbol = "$";

    /// <summary>Signo activo en esta instalación de Windows.</summary>
    public static string Symbol => _symbol;

    /// <summary>Cultura privada para exportaciones que requieren IFormatProvider.</summary>
    public static CultureInfo Culture => _culture;

    /// <summary>Formato numérico para Excel que mantiene los importes como números.</summary>
    public static string ExcelNumberFormat => $"\"{_symbol}\" #,##0.00;[Red]-\"{_symbol}\" #,##0.00;\"{_symbol}\" 0.00";

    /// <summary>Comprueba que el signo sea corto y no pueda alterar la sintaxis del formato Excel.</summary>
    /// <param name="symbol">Signo propuesto por el administrador.</param>
    /// <returns>Verdadero para letras, símbolos monetarios, espacios o barras hasta ocho caracteres.</returns>
    public static bool IsValidSymbol(string? symbol) =>
        !string.IsNullOrWhiteSpace(symbol) && symbol.Length <= 8
        && symbol == symbol.Trim()
        && symbol.All(c => char.IsLetter(c) || char.GetUnicodeCategory(c) == UnicodeCategory.CurrencySymbol
            || c is '/' or ' ');

    /// <summary>Cambia únicamente el formato visual de los próximos importes presentados.</summary>
    /// <param name="symbol">Signo de moneda validado.</param>
    public static void SetSymbol(string symbol)
    {
        if (!IsValidSymbol(symbol))
            throw new ArgumentException("Use un signo de moneda de hasta ocho caracteres, sin cifras ni separadores decimales.", nameof(symbol));
        _symbol = symbol;
        _culture = CreateCulture(symbol);
    }

    /// <summary>Presenta dos decimales sin modificar ni persistir el dato de negocio.</summary>
    /// <param name="amount">Importe decimal ya validado por las reglas de persistencia.</param>
    /// <returns>Texto con signo, separadores y dos decimales.</returns>
    public static string Format(decimal amount) => amount.ToString("C2", _culture);

    /// <summary>Prepara una cultura privada para que el signo no cambie la configuración de Windows.</summary>
    /// <param name="symbol">Signo de moneda visible.</param>
    /// <returns>Cultura independiente de la configuración regional del proceso.</returns>
    private static CultureInfo CreateCulture(string symbol)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.CurrencySymbol = symbol;
        culture.NumberFormat.CurrencyDecimalDigits = 2;
        return CultureInfo.ReadOnly(culture);
    }
}
