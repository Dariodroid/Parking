using Parking.Application.UseCases;

namespace Parking.UI.Windows.Services;

/// <summary>Conserva el último ticket y lo envía a la impresora configurada en este equipo.</summary>
public sealed class EntryTicketPrintService
{
    private readonly ThermalTicketPrinter _printer;
    private readonly ApplicationSettingsStore _settings;
    private EntryTicketData? _lastTicket;

    /// <summary>Indica si existe un ticket de entrada que pueda reimprimirse.</summary>
    public bool CanReprint => _lastTicket is not null;

    /// <summary>Recibe la impresora de Windows y los ajustes locales de impresión.</summary>
    public EntryTicketPrintService(ThermalTicketPrinter printer, ApplicationSettingsStore settings)
    {
        _printer = printer;
        _settings = settings;
    }

    /// <summary>Guarda el ticket para reimpresión y lo imprime si el operador activó esa opción.</summary>
    /// <returns>Un aviso cuando el guardado de la entrada fue correcto pero falló la impresión.</returns>
    public string? PrintIfConfigured(EntryTicketData? ticket)
    {
        if (ticket is null) return null;
        _lastTicket = ticket;
        var settings = _settings.Load();
        if (!settings.AutoPrintTickets) return null;

        try
        {
            _printer.Print(ticket, settings.TicketPrinterName, settings.TicketPaperWidthMm);
            return null;
        }
        catch (Exception error)
        {
            return $"Entrada guardada, pero no se pudo enviar el ticket a la impresora: {error.Message} Use Reimprimir último ticket.";
        }
    }

    /// <summary>Reenvía el último ticket con la configuración actual, sin crear otra sesión.</summary>
    public void ReprintLast()
    {
        if (_lastTicket is null) return;
        var settings = _settings.Load();
        _printer.Print(_lastTicket, settings.TicketPrinterName, settings.TicketPaperWidthMm);
    }
}
