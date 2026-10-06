using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using System.Globalization;

namespace Parking.UI.Windows.Services;

/// <summary>Construye los elementos visuales comunes de los informes Word.</summary>
internal static class ReportWordLayout
{
    private const string Navy = "17324D";
    private const string Blue = "245A81";
    private const string Pale = "EAF1F6";
    private const string Muted = "596B7A";

    /// <summary>Crea un DOCX vacío con su parte principal y cuerpo.</summary>
    /// <param name="path">Ruta donde se escribirá el documento.</param>
    /// <returns>Documento OpenXML abierto que debe cerrarse con using.</returns>
    internal static WordprocessingDocument NewWord(string path)
    {
        // OpenXML requiere una parte principal antes de insertar párrafos/tablas.
        var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        return document;
    }

    /// <summary>Obtiene el cuerpo creado para el informe o informa si el documento está incompleto.</summary>
    internal static W.Body GetBody(WordprocessingDocument document) =>
        document.MainDocumentPart?.Document?.Body
        ?? throw new InvalidOperationException("El documento Word no tiene un cuerpo disponible.");

    /// <summary>Guarda el documento principal una vez completado el informe.</summary>
    internal static void Save(WordprocessingDocument document)
    {
        var content = document.MainDocumentPart?.Document
            ?? throw new InvalidOperationException("El documento Word no tiene contenido principal.");
        content.Save();
    }

    /// <summary>Inserta identidad, título y fecha de emisión en el Word.</summary>
    /// <param name="body">Cuerpo del documento de destino.</param>
    /// <param name="title">Título del informe.</param>
    /// <param name="generated">Fecha y hora de generación.</param>
    internal static void WordHeading(W.Body body, string title, DateTime generated)
    {
        // El borde inferior cierra visualmente el encabezado.
        WordText(body, "SISTEMA DE GESTIÓN DE PARQUEADERO", true, 20, Navy);
        WordText(body, title, true, 32, Navy);
        WordText(body, $"Emitido: {generated:dd/MM/yyyy HH:mm}", false, 18, Muted);
        body.Append(new W.Paragraph(new W.ParagraphProperties(
            new W.ParagraphBorders(new W.BottomBorder { Val = W.BorderValues.Single, Color = Blue, Size = 16 }),
            new W.SpacingBetweenLines { After = "160" })));
    }

    /// <summary>Añade un subtítulo de sección al documento.</summary>
    /// <param name="body">Cuerpo donde se añade el texto.</param>
    /// <param name="title">Texto del subtítulo.</param>
    internal static void WordSection(W.Body body, string title) =>
        WordText(body, title, true, 22, Blue);

    /// <summary>Añade cierre institucional y configura página horizontal.</summary>
    /// <param name="body">Cuerpo al que se agrega el cierre.</param>
    internal static void WordFooter(W.Body body)
    {
        // El tamaño y los márgenes dejan espacio para la tabla detallada.
        WordText(body, "Documento generado por el sistema de gestión de parqueadero.", false, 16, Muted);
        body.Append(new W.SectionProperties(
            new W.PageSize { Width = 15840, Height = 12240, Orient = W.PageOrientationValues.Landscape },
            new W.PageMargin { Top = 720, Right = 720, Bottom = 720, Left = 720 }));
    }

    /// <summary>Inserta un párrafo con estilo tipográfico controlado.</summary>
    /// <param name="body">Cuerpo que recibe el párrafo.</param>
    /// <param name="text">Contenido literal del párrafo.</param>
    /// <param name="bold">Indica si se usa negrita.</param>
    /// <param name="size">Tamaño de fuente en medios puntos de OpenXML.</param>
    /// <param name="color">Color hexadecimal sin almohadilla.</param>
    internal static void WordText(W.Body body, string text, bool bold = false, int size = 19, string color = Navy)
    {
        // Preserve mantiene los espacios presentes en el texto recibido.
        body.Append(new W.Paragraph(
            new W.ParagraphProperties(new W.SpacingBetweenLines { After = "120" }),
            new W.Run(RunStyle(bold, size, color),
                new W.Text(text) { Space = SpaceProcessingModeValues.Preserve })));
    }

    /// <summary>Variante que reparte el ancho de tabla por igual entre sus columnas.</summary>
    /// <param name="headers">Títulos de las columnas.</param>
    /// <returns>Tabla con cabecera y anchos uniformes, aún sin filas de datos.</returns>
    internal static W.Table WordTable(params string[] headers) =>
        WordTable(headers, Enumerable.Repeat(14000 / headers.Length, headers.Length).ToArray());

    /// <summary>Variante que crea una tabla con anchos explícitos por columna.</summary>
    /// <param name="headers">Títulos en orden de izquierda a derecha.</param>
    /// <param name="widths">Anchura OpenXML de cada columna, en el mismo orden.</param>
    /// <returns>Tabla con propiedades, cuadrícula y cabecera preparada.</returns>
    internal static W.Table WordTable(IReadOnlyList<string> headers, IReadOnlyList<int> widths)
    {
        // La cuadrícula fija los anchos; la cabecera se repite al paginar.
        var table = new W.Table();
        table.Append(new W.TableProperties(
            new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct },
            new W.TableBorders(
                new W.BottomBorder { Val = W.BorderValues.Single, Color = "D4DFE7", Size = 4 },
                new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Color = "D4DFE7", Size = 4 })));
        table.Append(new W.TableGrid(widths.Select(width =>
            new W.GridColumn { Width = width.ToString(CultureInfo.InvariantCulture) })));
        var row = new W.TableRow(new W.TableRowProperties(new W.TableHeader()));
        foreach (var title in headers) row.Append(WordCell(title, true, Navy, "FFFFFF"));
        table.Append(row);
        return table;
    }

    /// <summary>Agrega una fila de datos y alterna su fondo.</summary>
    /// <param name="table">Tabla a la que se añade la fila.</param>
    /// <param name="values">Texto de cada columna en orden.</param>
    internal static void WordRow(W.Table table, params string[] values)
    {
        // La cantidad de filas previas determina el color de esta fila.
        var row = new W.TableRow();
        var shade = table.Elements<W.TableRow>().Count() % 2 == 0 ? Pale : "FFFFFF";
        foreach (var value in values) row.Append(WordCell(value, false, shade));
        table.Append(row);
    }

    /// <summary>Añade una fila final de resumen con estilo destacado.</summary>
    /// <param name="table">Tabla de destino.</param>
    /// <param name="values">Etiquetas y sumas en orden de columnas.</param>
    internal static void WordTotalRow(W.Table table, params string[] values)
    {
        // Todas las celdas del total usan fondo claro y negrita.
        var row = new W.TableRow();
        foreach (var value in values) row.Append(WordCell(value, true, Pale, Navy));
        table.Append(row);
    }

    /// <summary>Convierte texto, incluidos saltos de línea, en una celda Word.</summary>
    /// <param name="value">Contenido visible de la celda.</param>
    /// <param name="bold">Activa negrita para cabeceras o totales.</param>
    /// <param name="background">Color de fondo hexadecimal.</param>
    /// <param name="foreground">Color de letra hexadecimal.</param>
    /// <returns>Celda OpenXML con párrafo y sombreado.</returns>
    private static W.TableCell WordCell(string value, bool bold, string background, string foreground = Navy)
    {
        // Cada salto de línea se convierte en un Break dentro de la misma celda.
        var run = new W.Run(RunStyle(bold, 17, foreground));
        var lines = (value ?? "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) run.Append(new W.Break());
            run.Append(new W.Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        return new W.TableCell(
            new W.TableCellProperties(new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = background }),
            new W.Paragraph(
                new W.ParagraphProperties(new W.SpacingBetweenLines { After = "0" }),
                run));
    }

    /// <summary>Construye la fuente y color que comparten los textos Word.</summary>
    /// <param name="bold">Indica si debe añadirse negrita.</param>
    /// <param name="size">Tamaño OpenXML en medios puntos.</param>
    /// <param name="color">Color hexadecimal del texto.</param>
    /// <returns>Propiedades de un fragmento de texto OpenXML.</returns>
    private static W.RunProperties RunStyle(bool bold, int size, string color)
    {
        // Aptos se declara para texto ASCII y caracteres extendidos.
        var style = new W.RunProperties(new W.RunFonts { Ascii = "Aptos", HighAnsi = "Aptos" });
        if (bold) style.Append(new W.Bold());
        style.Append(new W.Color { Val = color });
        style.Append(new W.FontSize { Val = size.ToString(CultureInfo.InvariantCulture) });
        return style;
    }
}
