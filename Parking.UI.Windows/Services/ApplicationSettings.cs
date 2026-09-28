namespace Parking.UI.Windows.Services;

/// <summary>Preferencias locales que no se incluyen en el instalador.</summary>
/// <param name="ConnectionString">Conexión SQL de este equipo.</param>
/// <param name="Theme">Tema elegido por el usuario.</param>
public sealed record ApplicationSettings(string ConnectionString = "", AppThemeMode Theme = AppThemeMode.Dark);
