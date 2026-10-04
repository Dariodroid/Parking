using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        var document = CaptureCompleteSheet(sheet, pageWidth, pageHeight);
        // Al enviar solo imágenes inmutables, el controlador no vuelve a mirar la posición del scroll.
        dialog.PrintDocument(document.DocumentPaginator, jobName);
        return true;
    }

    /// <summary>Captura todas las páginas mientras la hoja está separada del visor con scroll.</summary>
    /// <param name="sheet">Hoja real mostrada en la app.</param>
    /// <param name="pageWidth">Ancho imprimible indicado por Windows.</param>
    /// <param name="pageHeight">Alto imprimible indicado por Windows.</param>
    /// <returns>Documento independiente de la posición actual de la interfaz.</returns>
    private static FixedDocument CaptureCompleteSheet(FrameworkElement sheet, double pageWidth, double pageHeight)
    {
        // La hoja de los tres informes es el contenido directo de su visor exterior.
        if (sheet.Parent is not ScrollViewer viewer || !ReferenceEquals(viewer.Content, sheet))
            throw new InvalidOperationException("No se pudo localizar el visor del informe.");
        sheet.UpdateLayout();
        double sourceWidth = sheet.ActualWidth;
        if (sourceWidth <= 0) throw new InvalidOperationException("La hoja del informe aún no está lista para imprimir.");

        // El DataContext heredado se conserva durante la medición fuera del visor.
        object? dataContext = sheet.DataContext;
        object localContext = sheet.ReadLocalValue(FrameworkElement.DataContextProperty);
        double horizontalOffset = viewer.HorizontalOffset;
        double verticalOffset = viewer.VerticalOffset;
        var host = new Canvas();
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);
        try
        {
            // Retirar la hoja elimina el recorte de ScrollViewer que causó la página parcial.
            viewer.Content = null;
            sheet.DataContext = dataContext;
            // Canvas reasigna el VisualOffset que ScrollViewer dejó negativo tras desplazarse.
            host.Children.Add(sheet);
            host.Measure(new Size(sourceWidth, double.PositiveInfinity));
            host.Arrange(new Rect(0, 0, sourceWidth, Math.Max(sheet.DesiredSize.Height, sheet.MinHeight)));
            sheet.UpdateLayout();
            double sourceHeight = sheet.ActualHeight;
            if (sourceHeight <= 0) throw new InvalidOperationException("La hoja del informe quedó sin contenido imprimible.");

            AddPages(document, sheet, sourceWidth, sourceHeight, pageWidth, pageHeight);
        }
        finally
        {
            // Se restituye la misma hoja, sus enlaces y la posición de lectura del usuario.
            host.Children.Remove(sheet);
            viewer.Content = sheet;
            if (localContext == DependencyProperty.UnsetValue)
                sheet.ClearValue(FrameworkElement.DataContextProperty);
            else
                sheet.SetValue(FrameworkElement.DataContextProperty, localContext);
            viewer.UpdateLayout();
            viewer.ScrollToHorizontalOffset(horizontalOffset);
            viewer.ScrollToVerticalOffset(verticalOffset);
        }
        return document;
    }

    /// <summary>Divide la hoja completa en páginas y procura no cortar filas del informe.</summary>
    private static void AddPages(FixedDocument document, FrameworkElement sheet,
        double sourceWidth, double sourceHeight, double pageWidth, double pageHeight)
    {
        const double margin = 18;
        double scale = Math.Min(1, (pageWidth - margin * 2) / sourceWidth);
        double sliceHeight = (pageHeight - margin * 2) / scale;
        var rowBreaks = FindRowBreaks(sheet);
        double offset = 0;

        while (offset < sourceHeight - 0.5)
        {
            double end = Math.Min(sourceHeight, offset + sliceHeight);
            if (end < sourceHeight)
            {
                double lowerBound = offset + sliceHeight * 0.65;
                double rowEnd = 0;
                foreach (double rowBreak in rowBreaks)
                {
                    if (rowBreak > lowerBound && rowBreak <= end && rowBreak > rowEnd)
                        rowEnd = rowBreak;
                }
                if (rowEnd > offset) end = rowEnd;
            }

            double height = end - offset;
            if (height <= 0) throw new InvalidOperationException("No se pudo dividir el informe en páginas.");
            // La imagen se conserva antes de devolver la hoja al visor.
            var image = RenderSlice(sheet, sourceWidth, offset, height);
            document.Pages.Add(CreatePage(image, pageWidth, pageHeight, sourceWidth, height, scale, margin));
            offset = end;
        }
    }

    /// <summary>Rasteriza una franja de la hoja independiente y congela el resultado.</summary>
    /// <param name="sheet">Hoja medida sin recorte exterior.</param>
    /// <param name="sourceWidth">Ancho original de la hoja.</param>
    /// <param name="offset">Punto vertical donde empieza esta página.</param>
    /// <param name="height">Altura original que cabe en esta página.</param>
    /// <returns>Imagen de alta resolución que no depende del árbol visual activo.</returns>
    private static BitmapSource RenderSlice(FrameworkElement sheet, double sourceWidth, double offset, double height)
    {
        // La vista de una franja se pinta a 192 DPI para conservar la lectura en A4.
        var brush = new VisualBrush(sheet)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, offset, sourceWidth, height),
            Stretch = Stretch.Fill
        };
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, sourceWidth, height));
            context.DrawRectangle(brush, null, new Rect(0, 0, sourceWidth, height));
        }
        const double dpi = 192;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(sourceWidth * dpi / 96),
            (int)Math.Ceiling(height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Crea una página de papel con una franja ya capturada de forma completa.</summary>
    /// <param name="source">Imagen inmutable de la franja.</param>
    /// <param name="pageWidth">Ancho imprimible indicado por Windows.</param>
    /// <param name="pageHeight">Alto imprimible indicado por Windows.</param>
    /// <param name="sourceWidth">Ancho original de la hoja.</param>
    /// <param name="height">Altura original capturada.</param>
    /// <param name="scale">Escala uniforme elegida para el papel.</param>
    /// <param name="margin">Espacio reservado alrededor del informe.</param>
    /// <returns>Página lista para integrarse al documento paginado.</returns>
    private static PageContent CreatePage(BitmapSource source, double pageWidth, double pageHeight,
        double sourceWidth, double height, double scale, double margin)
    {
        var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
        var visibleSlice = new Image
        {
            Width = sourceWidth * scale,
            Height = height * scale,
            Source = source,
            Stretch = Stretch.Fill
        };
        FixedPage.SetLeft(visibleSlice, margin);
        FixedPage.SetTop(visibleSlice, margin);
        page.Children.Add(visibleSlice);
        return new PageContent { Child = page };
    }

    /// <summary>Encuentra finales de filas renderizadas para evitar cortarlas entre dos páginas.</summary>
    /// <param name="sheet">Hoja que contiene la tabla del informe.</param>
    /// <returns>Coordenadas verticales de fin de fila relativas a la hoja.</returns>
    private static IReadOnlyList<double> FindRowBreaks(FrameworkElement sheet)
    {
        var breaks = new List<double>();
        CollectRows(sheet, sheet, breaks);
        return breaks;
    }

    /// <summary>Recorre la tabla visual y acumula los límites inferiores de sus filas.</summary>
    /// <param name="current">Elemento del árbol que se inspecciona.</param>
    /// <param name="sheet">Origen para convertir coordenadas.</param>
    /// <param name="breaks">Límites encontrados.</param>
    private static void CollectRows(DependencyObject current, FrameworkElement sheet, List<double> breaks)
    {
        if (current is DataGridRow row && row.ActualHeight > 0)
        {
            // El límite se mide en la hoja, aunque la tabla esté dentro de otro panel.
            double bottom = row.TranslatePoint(new Point(0, row.ActualHeight), sheet).Y;
            if (double.IsFinite(bottom)) breaks.Add(bottom);
        }
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
            CollectRows(VisualTreeHelper.GetChild(current, index), sheet, breaks);
    }
}
