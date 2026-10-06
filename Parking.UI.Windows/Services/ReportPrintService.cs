using System.Printing;
using System.Windows;
using System.Windows.Controls;

namespace Parking.UI.Windows.Services;

/// <summary>Imprime la hoja real del informe con su misma distribución, colores y datos visibles.</summary>
public sealed class ReportPrintService
{
    /// <summary>Pagina una hoja WPF completa y la envía a una impresora elegida en Windows.</summary>
    /// <param name="sheet">Hoja blanca del informe, sin los filtros ni barras de desplazamiento exteriores.</param>
    /// <param name="jobName">Título que identifica el trabajo en la cola de impresión.</param>
    /// <returns>Verdadero cuando el usuario confirmó la impresión; falso cuando canceló.</returns>
    public bool Print(FrameworkElement sheet, string jobName)
    {
        // La hoja ya contiene las tarjetas, tabla y total usados en pantalla.
        ArgumentNullException.ThrowIfNull(sheet);
        if (string.IsNullOrWhiteSpace(jobName)) throw new ArgumentException("Indique el nombre del informe.", nameof(jobName));

        // Windows permite escoger cualquier impresora instalada y ajustar sus opciones.
        var dialog = new PrintDialog();
        if (dialog.PrintTicket is { } ticket)
        {
            // Los tres informes tienen una hoja ancha y se leen mejor en horizontal.
            ticket.PageOrientation = PageOrientation.Landscape;
            dialog.PrintTicket = ticket;
        }
        if (dialog.ShowDialog() != true) return false;

        // El controlador informa el área que realmente admite tinta.
        double pageWidth = dialog.PrintableAreaWidth;
        double pageHeight = dialog.PrintableAreaHeight;
        if (!double.IsFinite(pageWidth) || !double.IsFinite(pageHeight) || pageWidth < 650 || pageHeight < 350)
            throw new InvalidOperationException("Seleccione papel tamaño A4 o superior para imprimir el informe completo y legible.");

        // Se toma una instantánea fuera del ScrollViewer: su recorte no debe decidir qué se imprime.
        var document = ReportSheetPaginator.Capture(sheet, pageWidth, pageHeight);
        // Al enviar solo imágenes inmutables, el controlador no vuelve a mirar la posición del scroll.
        dialog.PrintDocument(document.DocumentPaginator, jobName);
        return true;
    }
}
