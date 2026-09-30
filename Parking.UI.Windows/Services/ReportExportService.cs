using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Parking.Application.Dto;
using Parking.Domain.Model.Models;
using System.Globalization;

namespace Parking.UI.Windows.Services;

/// <summary>Presentación común para los informes descargables, sin alterar sus datos.</summary>
public sealed class ReportExportService
{
    private const string Navy = "17324D";
    private const string Blue = "245A81";
    private const string Pale = "EAF1F6";
    private const string Muted = "596B7A";
    private static string CurrencyFormat => CurrencyDisplay.ExcelNumberFormat;
    private static CultureInfo Culture => CurrencyDisplay.Culture;

    /// <summary>Genera la hoja de operadores con período, cobros y total recaudado.</summary>
    /// <param name="path">Ruta completa del archivo XLSX que se guardará.</param>
    /// <param name="source">Filas ya consultadas para el período aplicado.</param>
    /// <param name="from">Fecha inicial mostrada en el encabezado.</param>
    /// <param name="to">Fecha final mostrada en el encabezado.</param>
    /// <param name="generated">Momento de emisión del informe.</param>
    public void ExportOperatorsExcel(string path, IEnumerable<OperatorReportItem> source,
        DateTime from, DateTime to, DateTime generated)
    {
        // Se materializan las filas para reutilizarlas en cuerpo y totales.
        var items = source.ToList();
        // ClosedXML crea un libro nuevo sin consultar de nuevo la base de datos.
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("Operadores");
        // Las primeras filas identifican el informe y los filtros aplicados.
        ExcelHeading(sheet, "INFORME DE OPERADORES", 3, generated);
        sheet.Cell(4, 1).Value = "PERÍODO";
        sheet.Cell(4, 2).Value = $"{from:dd/MM/yyyy} al {to:dd/MM/yyyy}";
        sheet.Cell(5, 1).Value = "OPERADORES";
        sheet.Cell(5, 2).Value = items.Count;
        sheet.Cell(5, 3).Value = $"COBROS: {items.Sum(x => x.TotalPayments):N0}";
        ExcelHeader(sheet, 7, "Operador", "Cantidad de cobros", "Total recaudado");

        // Cada elemento ocupa una fila y recibe el estilo alternado común.
        var row = 8;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.OperatorName;
            sheet.Cell(row, 2).Value = item.TotalPayments;
            sheet.Cell(row, 3).Value = item.TotalAmount;
            ExcelBodyRow(sheet, row, 3);
            row++;
        }
        // El mensaje vacío y el total también se muestran sin resultados.
        if (items.Count == 0) ExcelEmpty(sheet, row, 3);
        var totalRow = Math.Max(row, 9) + 1;
        sheet.Cell(totalRow, 1).Value = "TOTAL GENERAL";
        sheet.Cell(totalRow, 2).Value = items.Sum(x => x.TotalPayments);
        sheet.Cell(totalRow, 3).Value = items.Sum(x => x.TotalAmount);
        ExcelTotal(sheet, totalRow, 3);
        // Los anchos y formatos hacen legibles nombres e importes en Excel.
        sheet.Column(1).Width = 38;
        sheet.Column(2).Width = 23;
        sheet.Column(3).Width = 25;
        sheet.Range(8, 3, Math.Max(row - 1, 8), 3).Style.NumberFormat.Format = CurrencyFormat;
        sheet.Cell(totalRow, 3).Style.NumberFormat.Format = CurrencyFormat;
        ExcelFinish(sheet, 7, Math.Max(row - 1, 7), 3);
        // Se guarda exactamente la instantánea recibida por este método.
        book.SaveAs(path);
    }

    /// <summary>Exporta a Excel los mismos vehículos y criterios aplicados en la vista previa.</summary>
    /// <param name="path">Ruta completa del XLSX de salida.</param>
    /// <param name="source">Filas visibles resultantes de la búsqueda aplicada.</param>
    /// <param name="filter">Copia del filtro usado al cargar esas filas.</param>
    /// <param name="generated">Fecha y hora de emisión.</param>
    public void ExportVehiclesExcel(string path, IEnumerable<VehicleReportDto> source,
        VehicleReportFilterDto filter, DateTime generated)
    {
        // Se toma una instantánea para que cuerpo y resumen cuenten lo mismo.
        var items = source.ToList();
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("Vehículos");
        ExcelHeading(sheet, "INFORME DETALLADO DE VEHÍCULOS", 12, generated);
        // El encabezado documenta período, criterios y alcance del resumen.
        sheet.Cell(4, 1).Value = "PERÍODO DE BÚSQUEDA";
        sheet.Cell(4, 3).Value = VehiclePeriod(filter);
        sheet.Cell(5, 1).Value = "CRITERIOS";
        sheet.Cell(5, 3).Value = VehicleCriteria(filter);
        sheet.Cell(6, 1).Value = "RESUMEN";
        sheet.Cell(6, 3).Value = $"{items.Count:N0} vehículos · Total cobrado mostrado: {items.Sum(x => x.TotalCollected).ToString("C", Culture)}";
        sheet.Range(6, 3, 6, 12).Merge();
        sheet.Cell(7, 1).Value = "Cuota = precio del plan; cobrado = pagos de estancias. Un cliente mensual puede ingresar con tarifa ocasional fuera de cobertura.";
        sheet.Range(7, 1, 7, 12).Merge();
        sheet.Range(7, 1, 7, 12).Style.Font.FontColor = XLColor.FromHtml("#596B7A");
        ExcelHeader(sheet, 9, "Placa", "Propietario", "Tipo", "Categoría", "Estado plan",
            "Mensualidad", "Ingresos", "Último ingreso", "Estado", "Total cobrado",
            "Minutos", "Última salida");

        // Las doce columnas conservan los datos concretos de cada resultado.
        var row = 10;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Plate;
            sheet.Cell(row, 2).Value = item.OwnerName;
            sheet.Cell(row, 3).Value = item.VehicleType;
            sheet.Cell(row, 4).Value = item.Category == "Mensual"
                ? $"Cliente mensual\n{item.AccessSummary}" : item.Category;
            var reportRow = new VehicleReportRow(row - 9, item);
            sheet.Cell(row, 5).Value = item.Category == "Mensual" && item.PlanEndDate.HasValue
                ? $"{reportRow.PlanStatusLabel}\nFin: {item.PlanEndDate:dd/MM/yyyy}"
                : reportRow.PlanStatusLabel;
            sheet.Cell(row, 6).Value = item.MonthlyFee;
            sheet.Cell(row, 7).Value = item.TotalEntries;
            if (item.LastEntryDate.HasValue) sheet.Cell(row, 8).Value = item.LastEntryDate.Value;
            sheet.Cell(row, 9).Value = item.CurrentStatus;
            sheet.Cell(row, 10).Value = item.TotalCollected;
            sheet.Cell(row, 11).Value = item.TotalMinutesParked;
            if (item.LastExitDate.HasValue) sheet.Cell(row, 12).Value = item.LastExitDate.Value;
            ExcelBodyRow(sheet, row, 12);
            // La modalidad ocupa una segunda línea para que no quede oculta en Excel.
            if (item.Category == "Mensual")
            {
                sheet.Cell(row, 4).Style.Alignment.WrapText = true;
                sheet.Cell(row, 5).Style.Alignment.WrapText = true;
                sheet.Row(row).Height = 49;
            }
            row++;
        }
        // La ausencia de filas se indica dentro de la misma tabla.
        if (items.Count == 0) ExcelEmpty(sheet, row, 12);
        var totalRow = Math.Max(row, 11) + 1;
        sheet.Cell(totalRow, 1).Value = "TOTAL GENERAL";
        sheet.Cell(totalRow, 7).Value = items.Sum(x => x.TotalEntries);
        sheet.Cell(totalRow, 10).Value = items.Sum(x => x.TotalCollected);
        sheet.Cell(totalRow, 11).Value = items.Sum(x => x.TotalMinutesParked);
        ExcelTotal(sheet, totalRow, 12);
        // Anchos y formatos especializados evitan fechas o importes ambiguos.
        sheet.Columns(1, 12).Width = 17;
        sheet.Column(2).Width = 30;
        sheet.Column(4).Width = 23;
        sheet.Column(5).Width = 20;
        sheet.Column(8).Width = 22;
        sheet.Column(12).Width = 22;
        sheet.Column(10).Width = 21;
        sheet.Range(10, 6, Math.Max(row - 1, 10), 6).Style.NumberFormat.Format = CurrencyFormat;
        sheet.Range(10, 10, Math.Max(row - 1, 10), 10).Style.NumberFormat.Format = CurrencyFormat;
        sheet.Range(10, 8, Math.Max(row - 1, 10), 8).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        sheet.Range(10, 12, Math.Max(row - 1, 10), 12).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        sheet.Cell(totalRow, 10).Style.NumberFormat.Format = CurrencyFormat;
        ExcelFinish(sheet, 9, Math.Max(row - 1, 9), 12);
        // El libro se escribe solo después de completar todos los estilos.
        book.SaveAs(path);
    }

    /// <summary>Genera el informe Word de operadores desde las filas ya consultadas.</summary>
    /// <param name="path">Ruta completa del DOCX.</param>
    /// <param name="source">Resultados para el período aplicado.</param>
    /// <param name="from">Inicio del período mostrado.</param>
    /// <param name="to">Fin del período mostrado.</param>
    /// <param name="generated">Momento de emisión.</param>
    public void ExportOperatorsWord(string path, IEnumerable<OperatorReportItem> source,
        DateTime from, DateTime to, DateTime generated)
    {
        // Word recibe el mismo conjunto de filas usado en resumen y total.
        var items = source.ToList();
        using var document = NewWord(path);
        var body = document.MainDocumentPart!.Document.Body!;
        WordHeading(body, "INFORME DE OPERADORES", generated);
        WordText(body, $"Período: {from:dd/MM/yyyy} al {to:dd/MM/yyyy}", true);
        WordText(body, $"{items.Count:N0} operadores  |  {items.Sum(x => x.TotalPayments):N0} cobros  |  Total recaudado: {items.Sum(x => x.TotalAmount).ToString("C", Culture)}", true);
        // La tabla contiene una fila por operador y un total al final.
        var table = WordTable("Operador", "Cantidad de cobros", "Total recaudado");
        foreach (var item in items)
            WordRow(table, item.OperatorName, item.TotalPayments.ToString("N0", Culture),
                item.TotalAmount.ToString("C", Culture));
        if (items.Count == 0) WordRow(table, "Sin resultados", "", "");
        WordTotalRow(table, "TOTAL GENERAL", items.Sum(x => x.TotalPayments).ToString("N0", Culture),
            items.Sum(x => x.TotalAmount).ToString("C", Culture));
        body.Append(table);
        WordFooter(body);
        // Se confirma la estructura OpenXML antes de cerrar el archivo.
        document.MainDocumentPart!.Document.Save();
    }

    /// <summary>Genera una única tabla Word con los vehículos de la vista previa.</summary>
    /// <param name="path">Ruta completa del DOCX.</param>
    /// <param name="source">Resultados visibles de la búsqueda aplicada.</param>
    /// <param name="filter">Filtro que produjo esas filas y aparece en el documento.</param>
    /// <param name="generated">Momento de emisión.</param>
    public void ExportVehiclesWord(string path, IEnumerable<VehicleReportDto> source,
        VehicleReportFilterDto filter, DateTime generated)
    {
        // Se evita que enumeraciones repetidas produzcan resúmenes diferentes.
        var items = source.ToList();
        using var document = NewWord(path);
        var body = document.MainDocumentPart!.Document.Body!;
        WordHeading(body, "INFORME DETALLADO DE VEHÍCULOS", generated);
        WordText(body, $"Período de búsqueda: {VehiclePeriod(filter)}", true);
        WordText(body, $"Criterios: {VehicleCriteria(filter)}");
        WordText(body, $"{items.Count:N0} vehículos  |  Total cobrado mostrado: {items.Sum(x => x.TotalCollected).ToString("C", Culture)}", true);
        WordText(body, "Cuota = precio del plan; cobrado = pagos de estancias. Un cliente mensual puede ingresar con tarifa ocasional fuera de cobertura.");

        WordSection(body, "VEHÍCULOS ENCONTRADOS");
        // Los anchos explícitos mantienen una sola tabla legible en paisaje.
        var table = WordTable(
            ["Placa", "Propietario", "Tipo / categoría", "Plan / cuota",
             "Ingresos / min", "Últimos movimientos", "Estado", "Cobrado"],
            [1200, 2300, 1800, 1500, 1500, 3000, 1100, 1600]);
        // VehicleReportRow reúne los textos que comparte Word con la vista.
        for (var i = 0; i < items.Count; i++)
        {
            var row = new VehicleReportRow(i + 1, items[i]);
            WordRow(table, row.Vehicle.Plate, row.Vehicle.OwnerName, row.TypeAndCategory,
                row.PlanAndFee, row.Activity, row.Movements, row.Vehicle.CurrentStatus, row.Collected);
        }
        if (items.Count == 0) WordRow(table, "Sin resultados", "", "", "", "", "", "", "");
        WordTotalRow(table, "TOTAL", $"{items.Count:N0} vehículos", "", "",
            $"{items.Sum(x => x.TotalEntries):N0} ingresos\n{items.Sum(x => x.TotalMinutesParked):N0} min",
            "", "", items.Sum(x => x.TotalCollected).ToString("C", Culture));
        body.Append(table);
        WordFooter(body);
        // La tabla completa y el pie se persisten como documento OpenXML.
        document.MainDocumentPart!.Document.Save();
    }

    /// <summary>Escribe los indicadores diarios y sus totales en una hoja Excel filtrable.</summary>
    /// <param name="path">Destino XLSX.</param>
    /// <param name="report">Misma instantánea mostrada en pantalla.</param>
    /// <param name="from">Primer día aplicado.</param>
    /// <param name="to">Último día aplicado.</param>
    public void ExportPerformanceExcel(string path, ParkingPerformanceReport report, DateTime from, DateTime to)
    {
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("Rendimiento");
        ExcelHeading(sheet, "OCUPACIÓN Y RECAUDACIÓN", 7, report.GeneratedAt);
        sheet.Cell(4, 1).Value = "PERÍODO";
        sheet.Cell(4, 2).Value = $"{from:dd/MM/yyyy} al {to:dd/MM/yyyy}";
        sheet.Cell(5, 1).Value = "CAPACIDAD ACTUAL";
        sheet.Cell(5, 2).Value = report.Capacity;
        sheet.Cell(6, 1).Value = "RESUMEN";
        sheet.Cell(6, 2).Value = $"{report.TotalEntries:N0} entradas · {report.TotalExits:N0} salidas · "
            + $"{report.OccupancyPercent:N1}% ocupación · {report.TotalCollected.ToString("C", Culture)} cobrados";
        sheet.Range(6, 2, 6, 7).Merge();
        ExcelHeader(sheet, 8, "Día", "Entradas", "Salidas", "Ocupación", "Permanencia media",
            "Mayor demanda de entrada", "Cobrado");
        int row = 9;
        foreach (var day in report.Days)
        {
            sheet.Cell(row, 1).Value = day.Date;
            sheet.Cell(row, 2).Value = day.Entries;
            sheet.Cell(row, 3).Value = day.Exits;
            sheet.Cell(row, 4).Value = day.OccupancyPercent / 100;
            sheet.Cell(row, 5).Value = day.AverageStayMinutes;
            sheet.Cell(row, 6).Value = day.PeakEntryLabel;
            sheet.Cell(row, 7).Value = day.Collected;
            ExcelBodyRow(sheet, row, 7);
            row++;
        }
        if (report.Days.Count == 0) ExcelEmpty(sheet, row, 7);
        int totalRow = Math.Max(row, 10) + 1;
        sheet.Cell(totalRow, 1).Value = "TOTAL / PROMEDIO";
        sheet.Cell(totalRow, 2).Value = report.TotalEntries;
        sheet.Cell(totalRow, 3).Value = report.TotalExits;
        sheet.Cell(totalRow, 4).Value = report.OccupancyPercent / 100;
        sheet.Cell(totalRow, 7).Value = report.TotalCollected;
        ExcelTotal(sheet, totalRow, 7);
        sheet.Column(1).Width = 18;
        sheet.Columns(2, 3).Width = 15;
        sheet.Column(4).Width = 18;
        sheet.Column(5).Width = 23;
        sheet.Column(6).Width = 29;
        sheet.Column(7).Width = 21;
        sheet.Range(9, 1, Math.Max(row - 1, 9), 1).Style.DateFormat.Format = "dd/mm/yyyy";
        sheet.Range(9, 4, totalRow, 4).Style.NumberFormat.Format = "0.0%";
        sheet.Range(9, 7, totalRow, 7).Style.NumberFormat.Format = CurrencyFormat;
        ExcelFinish(sheet, 8, Math.Max(row - 1, 8), 7);
        sheet.Cell(totalRow + 2, 1).Value =
            "Actividad y ocupación: sesiones. Cobrado: únicamente payments por fecha de cobro. Se incluye cada día del período, aunque no haya pagos.";
        sheet.Range(totalRow + 2, 1, totalRow + 2, 7).Merge();
        book.SaveAs(path);
    }

    /// <summary>Genera un documento Word con el mismo resumen y detalle diario de la pantalla.</summary>
    /// <param name="path">Destino DOCX.</param>
    /// <param name="report">Instantánea aplicada.</param>
    /// <param name="from">Primer día aplicado.</param>
    /// <param name="to">Último día aplicado.</param>
    public void ExportPerformanceWord(string path, ParkingPerformanceReport report, DateTime from, DateTime to)
    {
        using var document = NewWord(path);
        var body = document.MainDocumentPart!.Document.Body!;
        WordHeading(body, "OCUPACIÓN Y RECAUDACIÓN", report.GeneratedAt);
        WordText(body, $"Período: {from:dd/MM/yyyy} al {to:dd/MM/yyyy}  |  Capacidad actual: {report.Capacity}", true);
        WordText(body, $"{report.TotalEntries:N0} entradas  |  {report.TotalExits:N0} salidas  |  "
            + $"{report.OccupancyPercent:N1}% ocupación  |  {report.TotalCollected.ToString("C", Culture)} cobrados", true);
        WordText(body, "Actividad y ocupación: sesiones. Cobrado: únicamente payments por fecha de cobro. Se incluye cada día del período, aunque no haya pagos.");
        WordSection(body, "EVOLUCIÓN DIARIA");
        var table = WordTable("Día", "Entradas", "Salidas", "Ocupación",
            "Estancia media", "Hora pico", "Cobrado");
        foreach (var day in report.Days)
            WordRow(table, day.Date.ToString("dd/MM/yyyy"), day.Entries.ToString("N0", Culture),
                day.Exits.ToString("N0", Culture), $"{day.OccupancyPercent:N1}%",
                $"{day.AverageStayMinutes:N0} min", day.PeakEntryLabel, day.Collected.ToString("C", Culture));
        if (report.Days.Count == 0) WordRow(table, "Sin resultados", "", "", "", "", "", "");
        WordTotalRow(table, "TOTAL / PROM.", report.TotalEntries.ToString("N0", Culture),
            report.TotalExits.ToString("N0", Culture), $"{report.OccupancyPercent:N1}%", "", "",
            report.TotalCollected.ToString("C", Culture));
        body.Append(table);
        WordFooter(body);
        document.MainDocumentPart.Document.Save();
    }

    /// <summary>Describe el rango de fechas aplicado al informe de vehículos.</summary>
    /// <param name="filter">Filtro cuyo período debe imprimirse.</param>
    /// <returns>Texto con fecha inicial y final, o indicación de extremo ausente.</returns>
    public static string VehiclePeriod(VehicleReportFilterDto filter) =>
        $"{filter.FromDate?.ToString("dd/MM/yyyy") ?? "Sin inicio"} al {filter.ToDate?.ToString("dd/MM/yyyy") ?? "Sin fin"}";
    /// <summary>Convierte categorías y estados seleccionados en un texto legible.</summary>
    /// <param name="filter">Filtro aplicado a las filas del informe.</param>
    /// <returns>Resumen textual de placa, propietario, categorías y estados.</returns>
    public static string VehicleCriteria(VehicleReportFilterDto filter) =>
        $"Placa: {Value(filter.Plate)} · Propietario: {Value(filter.OwnerName)} · " +
        $"Categorías: {Choices(filter.IncludeMonthly, filter.IncludeOccasional, "Mensual", "Ocasional")} · " +
        $"Estado: {Choices(filter.IncludeInside, filter.IncludeOutside, "Dentro", "Fuera")}";
    /// <summary>Presenta Todos cuando un criterio de texto está vacío.</summary>
    /// <param name="value">Texto introducido en el filtro.</param>
    /// <returns>Texto recortado o Todos.</returns>
    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "Todos" : value.Trim();
    /// <summary>Resume una pareja de opciones booleanas del filtro.</summary>
    /// <param name="first">Indica si se incluyó la primera opción.</param>
    /// <param name="second">Indica si se incluyó la segunda opción.</param>
    /// <param name="firstName">Nombre visible de la primera opción.</param>
    /// <param name="secondName">Nombre visible de la segunda opción.</param>
    /// <returns>Todos, una opción concreta o Según búsqueda.</returns>
    private static string Choices(bool first, bool second, string firstName, string secondName) =>
        first && second ? "Todos" : first ? firstName : second ? secondName : "Según búsqueda";

    /// <summary>Diseña las tres primeras filas y el rótulo de criterios del XLSX.</summary>
    /// <param name="sheet">Hoja que recibirá el encabezado.</param>
    /// <param name="title">Título del tipo de informe.</param>
    /// <param name="columns">Cantidad de columnas que abarcarán los títulos.</param>
    /// <param name="generated">Fecha y hora que se imprimen en la hoja.</param>
    private static void ExcelHeading(IXLWorksheet sheet, string title, int columns, DateTime generated)
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
    private static void ExcelHeader(IXLWorksheet sheet, int row, params string[] titles)
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
    private static void ExcelBodyRow(IXLWorksheet sheet, int row, int columns)
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
    private static void ExcelEmpty(IXLWorksheet sheet, int row, int columns)
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
    private static void ExcelTotal(IXLWorksheet sheet, int row, int columns)
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
    private static void ExcelFinish(IXLWorksheet sheet, int headerRow, int lastDataRow, int columns)
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

    /// <summary>Crea un DOCX vacío con su parte principal y cuerpo.</summary>
    /// <param name="path">Ruta donde se escribirá el documento.</param>
    /// <returns>Documento OpenXML abierto que debe cerrarse con using.</returns>
    private static WordprocessingDocument NewWord(string path)
    {
        // OpenXML requiere una parte principal antes de insertar párrafos/tablas.
        var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        return document;
    }

    /// <summary>Inserta identidad, título y fecha de emisión en el Word.</summary>
    /// <param name="body">Cuerpo del documento de destino.</param>
    /// <param name="title">Título del informe.</param>
    /// <param name="generated">Fecha y hora de generación.</param>
    private static void WordHeading(W.Body body, string title, DateTime generated)
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
    private static void WordSection(W.Body body, string title) =>
        WordText(body, title, true, 22, Blue);

    /// <summary>Añade cierre institucional y configura página horizontal.</summary>
    /// <param name="body">Cuerpo al que se agrega el cierre.</param>
    private static void WordFooter(W.Body body)
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
    private static void WordText(W.Body body, string text, bool bold = false, int size = 19, string color = Navy)
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
    private static W.Table WordTable(params string[] headers) =>
        WordTable(headers, Enumerable.Repeat(14000 / headers.Length, headers.Length).ToArray());

    /// <summary>Variante que crea una tabla con anchos explícitos por columna.</summary>
    /// <param name="headers">Títulos en orden de izquierda a derecha.</param>
    /// <param name="widths">Anchura OpenXML de cada columna, en el mismo orden.</param>
    /// <returns>Tabla con propiedades, cuadrícula y cabecera preparada.</returns>
    private static W.Table WordTable(IReadOnlyList<string> headers, IReadOnlyList<int> widths)
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
    private static void WordRow(W.Table table, params string[] values)
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
    private static void WordTotalRow(W.Table table, params string[] values)
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
