namespace Parking.UI.Windows.Services;

/// <summary>Preferencias locales que no se incluyen en el instalador.</summary>
/// <param name="ConnectionString">Conexión SQL de este equipo.</param>
/// <param name="Theme">Tema elegido por el usuario.</param>
/// <param name="CurrencySymbol">Signo visual de los importes; no convierte ni modifica cantidades.</param>
/// <param name="TicketPrinterName">Nombre de la impresora Windows elegida para tickets.</param>
/// <param name="TicketPaperWidthMm">Ancho del rollo térmico configurado en el controlador.</param>
/// <param name="AutoPrintTickets">Indica si los tickets ocasionales se envían tras guardar la entrada.</param>
public sealed record ApplicationSettings(string ConnectionString = "", AppThemeMode Theme = AppThemeMode.Dark,
    string CurrencySymbol = "$", string TicketPrinterName = "", int TicketPaperWidthMm = 80,
    bool AutoPrintTickets = false);
