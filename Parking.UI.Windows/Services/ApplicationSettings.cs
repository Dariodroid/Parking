namespace Parking.UI.Windows.Services;

/// <summary>Preferencias locales que no se incluyen en el instalador.</summary>
/// <param name="ConnectionString">Conexión SQL de este equipo.</param>
/// <param name="Theme">Tema elegido por el usuario.</param>
/// <param name="CurrencySymbol">Signo visual de los importes; no convierte ni modifica cantidades.</param>
public sealed record ApplicationSettings(string ConnectionString = "", AppThemeMode Theme = AppThemeMode.Dark,
    string CurrencySymbol = "$");
