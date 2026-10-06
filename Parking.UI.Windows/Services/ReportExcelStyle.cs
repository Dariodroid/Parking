using ClosedXML.Excel;

namespace Parking.UI.Windows.Services;

/// <summary>Aplica el formato común a las hojas Excel de los informes.</summary>
internal static class ReportExcelStyle
{
    /// <summary>Diseña las tres primeras filas y el rótulo de criterios del XLSX.</summary>
    /// <param name="sheet">Hoja que recibirá el encabezado.</param>
    /// <param name="title">Título del tipo de informe.</param>
    /// <param name="columns">Cantidad de columnas que abarcarán los títulos.</param>
    /// <param name="generated">Fecha y hora que se imprimen en la hoja.</param>
    internal static void Heading(IXLWorksheet sheet, string title, int columns, DateTime generated)
    {
        // Se oculta la cuadrícula nativa para que el documento parezca un informe.
        sheet.ShowGridLines = false;
        // La primera banda contiene la identidad del sistema.
        sheet.Cell(1, 1).Value = "SISTEMA DE GESTIÓN DE PARQUEADERO";
        sheet.Range(1, 1, 1, columns).Merge();
        sheet.Range(1, 1, 1, columns).Style.Fill.BackgroundColor = XLColor.FromHtml("#17324D");
        sheet.Range(1, 1, 1, columns).Style.Font.FontColor = XLColor.White;
        sheet.Range(1, 1, 1, columns).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, columns).Style.Font.FontSize = 12;
        sheet.Row(1).Height = 27;
        // La segunda banda lleva el título específico del informe.
        sheet.Cell(2, 1).Value = title;
        sheet.Range(2, 1, 2, columns).Merge();
        sheet.Range(2, 1, 2, columns).Style.Font.Bold = true;
        sheet.Range(2, 1, 2, columns).Style.Font.FontSize = 18;
        sheet.Range(2, 1, 2, columns).Style.Font.FontColor = XLColor.FromHtml("#17324D");
        sheet.Row(2).Height = 34;
        // Se conserva la hora exacta de generación para trazabilidad.
        sheet.Cell(3, 1).Value = $"Emitido: {generated:dd/MM/yyyy HH:mm}";
        sheet.Range(3, 1, 3, columns).Merge();
        sheet.Range(3, 1, 3, columns).Style.Font.FontColor = XLColor.FromHtml("#596B7A");
        sheet.Range(4, 1, 6, 1).Style.Font.Bold = true;
    }

    /// <summary>Escribe y estiliza los títulos de columnas de una tabla Excel.</summary>
    /// <param name="sheet">Hoja que contiene la tabla.</param>
    /// <param name="row">Número de la fila de encabezado.</param>
    /// <param name="titles">Títulos en el mismo orden de las columnas de datos.</param>
    internal static void Header(IXLWorksheet sheet, int row, params string[] titles)
    {
        // Cada título se escribe en su columna, empezando por la primera.
        for (var i = 0; i < titles.Length; i++) sheet.Cell(row, i + 1).Value = titles[i];
        // La banda azul distingue cabecera de cuerpo.
        var range = sheet.Range(row, 1, row, titles.Length);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#245A81");
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        sheet.Row(row).Height = 32;
    }

    /// <summary>Aplica altura y alternancia visual a una fila de datos.</summary>
    /// <param name="sheet">Hoja que contiene la fila.</param>
    /// <param name="row">Fila que se estiliza.</param>
    /// <param name="columns">Última columna incluida en el estilo.</param>
    internal static void BodyRow(IXLWorksheet sheet, int row, int columns)
    {
        // El sombreado de filas pares facilita seguir un registro ancho.
        var range = sheet.Range(row, 1, row, columns);
        if (row % 2 == 0) range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F8");
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(row).Height = 23;
    }

    /// <summary>Coloca un mensaje cuando los filtros no devolvieron filas.</summary>
    /// <param name="sheet">Hoja del informe.</param>
    /// <param name="row">Fila disponible para el mensaje.</param>
    /// <param name="columns">Número de columnas que ocupa el mensaje.</param>
    internal static void Empty(IXLWorksheet sheet, int row, int columns)
    {
        // La celda fusionada evita que parezca un registro incompleto.
        sheet.Cell(row, 1).Value = "Sin resultados para los criterios seleccionados";
        sheet.Range(row, 1, row, columns).Merge();
        sheet.Range(row, 1, row, columns).Style.Font.FontColor = XLColor.FromHtml("#596B7A");
    }

    /// <summary>Destaca la fila de totales con fondo y borde propios.</summary>
    /// <param name="sheet">Hoja del informe.</param>
    /// <param name="row">Fila de totales.</param>
    /// <param name="columns">Cantidad de columnas que cubre el formato.</param>
    internal static void Total(IXLWorksheet sheet, int row, int columns)
    {
        // El borde superior separa sumas y registros individuales.
        var range = sheet.Range(row, 1, row, columns);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1F6");
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#17324D");
        range.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        range.Style.Border.TopBorderColor = XLColor.FromHtml("#245A81");
        sheet.Row(row).Height = 28;
    }

    /// <summary>Activa filtros, fija la cabecera y configura la impresión de Excel.</summary>
    /// <param name="sheet">Hoja terminada.</param>
    /// <param name="headerRow">Fila que contiene los nombres de columnas.</param>
    /// <param name="lastDataRow">Última fila de datos incluida en el autofiltro.</param>
    /// <param name="columns">Última columna incluida en la tabla.</param>
    internal static void Finish(IXLWorksheet sheet, int headerRow, int lastDataRow, int columns)
    {
        // El autofiltro permite explorar la tabla exportada.
        sheet.Range(headerRow, 1, lastDataRow, columns).SetAutoFilter();
        // La cabecera permanece visible al bajar por muchos registros.
        sheet.SheetView.FreezeRows(headerRow);
        // El informe se imprime horizontalmente y repite títulos en cada página.
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, headerRow);
    }
}
