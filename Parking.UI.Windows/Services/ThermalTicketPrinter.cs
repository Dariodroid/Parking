using Parking.Application.UseCases;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Parking.UI.Windows.Services;

/// <summary>Imprime un ticket QR mediante cualquier controlador de impresora instalado en Windows.</summary>
public sealed class ThermalTicketPrinter
{
    /// <summary>Enumera las colas disponibles para que el administrador escoja una.</summary>
    /// <returns>Nombres ordenados de impresoras instaladas para el usuario.</returns>
    public IReadOnlyList<string> GetPrinterNames()
    {
        // Windows proporciona el controlador, de modo que no se acopla el sistema a ESC/POS.
        using var server = new LocalPrintServer();
        return server.GetPrintQueues().Select(queue => queue.Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    /// <summary>Envía al spooler el ticket de una entrada ya confirmada en SQL.</summary>
    /// <param name="ticket">Datos inmutables de la entrada y ruta del QR correspondiente.</param>
    /// <param name="printerName">Cola Windows elegida en Configuración.</param>
    /// <param name="paperWidthMm">Ancho de papel admitido: 58 u 80 milímetros.</param>
    public void Print(EntryTicketData ticket, string printerName, int paperWidthMm)
    {
        // La falta de configuración no debe traducirse en un ticket enviado a otra impresora.
        if (string.IsNullOrWhiteSpace(printerName))
            throw new InvalidOperationException("Configure una impresora de tickets en Configuración.");
        if (paperWidthMm is not (58 or 80))
            throw new ArgumentOutOfRangeException(nameof(paperWidthMm), "El ancho debe ser 58 u 80 mm.");
        if (!File.Exists(ticket.QrImagePath))
            throw new FileNotFoundException("No se encontró el QR de esta entrada.", ticket.QrImagePath);

        // La cola se vuelve a consultar al imprimir para detectar una impresora desconectada.
        using var server = new LocalPrintServer();
        using var queue = server.GetPrintQueues().FirstOrDefault(item =>
            string.Equals(item.Name, printerName, StringComparison.OrdinalIgnoreCase));
        if (queue is null)
            throw new InvalidOperationException($"La impresora '{printerName}' no está instalada en Windows.");

        // WPF mide en unidades de 1/96 de pulgada; el margen evita cortar letras y QR.
        double width = paperWidthMm / 25.4 * 96;
        FrameworkElement visual = BuildTicket(ticket, width - 16);
        visual.Measure(new Size(width - 16, double.PositiveInfinity));
        visual.Arrange(new Rect(8, 8, width - 16, visual.DesiredSize.Height));
        visual.UpdateLayout();

        // Se reserva además un tramo final para el área que el controlador no imprime.
        // Así el pie no queda pegado al borde inferior de la página personalizada.
        var printTicket = queue.DefaultPrintTicket.Clone();
        printTicket.PageMediaSize = new PageMediaSize(width,
            visual.DesiredSize.Height + 16 + 8 / 25.4 * 96);
        var dialog = new PrintDialog { PrintQueue = queue, PrintTicket = printTicket };
        dialog.PrintVisual(visual, $"Entrada {ticket.SessionCode}");
    }

    /// <summary>Compone una tira legible que muestra placa, ingreso y QR de salida.</summary>
    /// <param name="ticket">Sesión que se imprimirá.</param>
    /// <param name="contentWidth">Ancho útil después de descontar márgenes.</param>
    /// <returns>Visual WPF listo para la cola de impresión.</returns>
    private static FrameworkElement BuildTicket(EntryTicketData ticket, double contentWidth)
    {
        // La imagen se carga completamente para que el archivo no permanezca bloqueado.
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(ticket.QrImagePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        var content = new StackPanel { Width = contentWidth, Background = Brushes.White };
        content.Children.Add(Text("SISTEMA DE PARQUEADERO", 14, FontWeights.Bold));
        content.Children.Add(Text("TICKET DE ENTRADA", 12, FontWeights.SemiBold));
        content.Children.Add(Text("────────────────────────", 10, FontWeights.Normal));
        content.Children.Add(Text($"PLACA  {ticket.Plate}", 17, FontWeights.Bold));
        content.Children.Add(Text($"PUESTO  {ticket.SlotNumber}", 12, FontWeights.SemiBold));
        content.Children.Add(Text($"INGRESO  {ticket.EntryTime:dd/MM/yyyy HH:mm}", 11, FontWeights.Normal));
        content.Children.Add(Text($"CÓDIGO  {ticket.SessionCode}", 9, FontWeights.Normal));
        content.Children.Add(new Image
        {
            Source = image,
            Width = Math.Min(contentWidth - 10, 190),
            Height = Math.Min(contentWidth - 10, 190),
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 5)
        });
        content.Children.Add(Text("Conserve este ticket para la salida", 10, FontWeights.SemiBold));
        content.Children.Add(Text("El cobro se calcula al salir", 10, FontWeights.Normal));
        // El avance extra mantiene ambas líneas sobre la zona imprimible y deja papel para cortar.
        content.Children.Add(new Border { Height = 12 / 25.4 * 96 });
        return content;
    }

    /// <summary>Crea una línea centrada con contraste apto para papel térmico.</summary>
    /// <param name="value">Texto que se imprimirá.</param>
    /// <param name="fontSize">Tamaño en unidades WPF.</param>
    /// <param name="fontWeight">Peso visual de la línea.</param>
    /// <returns>Bloque con salto de línea y ajuste si el papel es estrecho.</returns>
    private static TextBlock Text(string value, double fontSize, FontWeight fontWeight) => new()
    {
        Text = value,
        Foreground = Brushes.Black,
        FontFamily = new FontFamily("Arial"),
        FontSize = fontSize,
        FontWeight = fontWeight,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 2)
    };
}
