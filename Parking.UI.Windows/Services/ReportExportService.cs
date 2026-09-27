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
    private const string CurrencyFormat = "$ #,##0.00";
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public void ExportOperatorsExcel(string path, IEnumerable<OperatorReportItem> source,
        DateTime from, DateTime to, DateTime generated)
    {
        var items = source.ToList();
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("Operadores");
        ExcelHeading(sheet, "INFORME DE OPERADORES", 3, generated);
        sheet.Cell(4, 1).Value = "PERÍODO";
        sheet.Cell(4, 2).Value = $"{from:dd/MM/yyyy} al {to:dd/MM/yyyy}";
        sheet.Cell(5, 1).Value = "OPERADORES";
        sheet.Cell(5, 2).Value = items.Count;
        sheet.Cell(5, 3).Value = $"COBROS: {items.Sum(x => x.TotalPayments):N0}";
        ExcelHeader(sheet, 7, "Operador", "Cantidad de cobros", "Total recaudado");

        var row = 8;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.OperatorName;
            sheet.Cell(row, 2).Value = item.TotalPayments;
            sheet.Cell(row, 3).Value = item.TotalAmount;
            ExcelBodyRow(sheet, row, 3);
            row++;
        }
        if (items.Count == 0) ExcelEmpty(sheet, row, 3);
        var totalRow = Math.Max(row, 9) + 1;
        sheet.Cell(totalRow, 1).Value = "TOTAL GENERAL";
        sheet.Cell(totalRow, 2).Value = items.Sum(x => x.TotalPayments);
        sheet.Cell(totalRow, 3).Value = items.Sum(x => x.TotalAmount);
        ExcelTotal(sheet, totalRow, 3);
        sheet.Column(1).Width = 38;
        sheet.Column(2).Width = 23;
        sheet.Column(3).Width = 25;
        sheet.Range(8, 3, Math.Max(row - 1, 8), 3).Style.NumberFormat.Format = CurrencyFormat;
        sheet.Cell(totalRow, 3).Style.NumberFormat.Format = CurrencyFormat;
        ExcelFinish(sheet, 7, Math.Max(row - 1, 7), 3);
        book.SaveAs(path);
    }

    public void ExportVehiclesExcel(string path, IEnumerable<VehicleReportDto> source,
        VehicleReportFilterDto filter, DateTime generated)
    {
        var items = source.ToList();
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("Vehículos");
        ExcelHeading(sheet, "INFORME DETALLADO DE VEHÍCULOS", 12, generated);
        sheet.Cell(4, 1).Value = "PERÍODO DE BÚSQUEDA";
        sheet.Cell(4, 3).Value = VehiclePeriod(filter);
        sheet.Cell(5, 1).Value = "CRITERIOS";
        sheet.Cell(5, 3).Value = VehicleCriteria(filter);
        sheet.Cell(6, 1).Value = "RESUMEN";
        sheet.Cell(6, 3).Value = $"{items.Count:N0} vehículos · Total cobrado mostrado: {items.Sum(x => x.TotalCollected).ToString("C", Culture)}";
        sheet.Range(6, 3, 6, 12).Merge();
        sheet.Cell(7, 1).Value = "Nota: mensuales muestran actividad histórica; ocasionales, actividad del período seleccionado.";
        sheet.Range(7, 1, 7, 12).Merge();
        sheet.Range(7, 1, 7, 12).Style.Font.FontColor = XLColor.FromHtml("#596B7A");
        ExcelHeader(sheet, 9, "Placa", "Propietario", "Tipo", "Categoría", "Estado plan",
            "Mensualidad", "Ingresos", "Último ingreso", "Estado", "Total cobrado",
            "Minutos", "Última salida");

        var row = 10;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Plate;
            sheet.Cell(row, 2).Value = item.OwnerName;
            sheet.Cell(row, 3).Value = item.VehicleType;
            sheet.Cell(row, 4).Value = item.Category;
            sheet.Cell(row, 5).Value = item.PlanStatus;
            sheet.Cell(row, 6).Value = item.MonthlyFee;
            sheet.Cell(row, 7).Value = item.TotalEntries;
            if (item.LastEntryDate.HasValue) sheet.Cell(row, 8).Value = item.LastEntryDate.Value;
            sheet.Cell(row, 9).Value = item.CurrentStatus;
            sheet.Cell(row, 10).Value = item.TotalCollected;
            sheet.Cell(row, 11).Value = item.TotalMinutesParked;
            if (item.LastExitDate.HasValue) sheet.Cell(row, 12).Value = item.LastExitDate.Value;
            ExcelBodyRow(sheet, row, 12);
            row++;
        }
        if (items.Count == 0) ExcelEmpty(sheet, row, 12);
        var totalRow = Math.Max(row, 11) + 1;
        sheet.Cell(totalRow, 1).Value = "TOTAL GENERAL";
        sheet.Cell(totalRow, 7).Value = items.Sum(x => x.TotalEntries);
        sheet.Cell(totalRow, 10).Value = items.Sum(x => x.TotalCollected);
        sheet.Cell(totalRow, 11).Value = items.Sum(x => x.TotalMinutesParked);
        ExcelTotal(sheet, totalRow, 12);
        sheet.Columns(1, 12).Width = 17;
        sheet.Column(2).Width = 30;
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
        book.SaveAs(path);
    }

    public void ExportOperatorsWord(string path, IEnumerable<OperatorReportItem> source,
        DateTime from, DateTime to, DateTime generated)
    {
        var items = source.ToList();
        using var document = NewWord(path);
        var body = document.MainDocumentPart!.Document.Body!;
        WordHeading(body, "INFORME DE OPERADORES", generated);
        WordText(body, $"Período: {from:dd/MM/yyyy} al {to:dd/MM/yyyy}", true);
        WordText(body, $"{items.Count:N0} operadores  |  {items.Sum(x => x.TotalPayments):N0} cobros  |  Total recaudado: {items.Sum(x => x.TotalAmount).ToString("C", Culture)}", true);
        var table = WordTable("Operador", "Cantidad de cobros", "Total recaudado");
        foreach (var item in items)
            WordRow(table, item.OperatorName, item.TotalPayments.ToString("N0", Culture),
                item.TotalAmount.ToString("C", Culture));
        if (items.Count == 0) WordRow(table, "Sin resultados", "", "");
        WordTotalRow(table, "TOTAL GENERAL", items.Sum(x => x.TotalPayments).ToString("N0", Culture),
            items.Sum(x => x.TotalAmount).ToString("C", Culture));
        body.Append(table);
        WordFooter(body);
        document.MainDocumentPart!.Document.Save();
    }

    public void ExportVehiclesWord(string path, IEnumerable<VehicleReportDto> source,
        VehicleReportFilterDto filter, DateTime generated)
    {
        var items = source.ToList();
        using var document = NewWord(path);
        var body = document.MainDocumentPart!.Document.Body!;
        WordHeading(body, "INFORME DETALLADO DE VEHÍCULOS", generated);
        WordText(body, $"Período de búsqueda: {VehiclePeriod(filter)}", true);
        WordText(body, $"Criterios: {VehicleCriteria(filter)}");
        WordText(body, $"{items.Count:N0} vehículos  |  Total cobrado mostrado: {items.Sum(x => x.TotalCollected).ToString("C", Culture)}", true);
        WordText(body, "Los mensuales muestran actividad histórica; los ocasionales, actividad del período seleccionado.");

        WordSection(body, "VEHÍCULOS ENCONTRADOS");
        var table = WordTable(
            ["Placa", "Propietario", "Tipo / categoría", "Plan / cuota",
             "Ingresos / min", "Últimos movimientos", "Estado", "Cobrado"],
            [1200, 2300, 1800, 1500, 1500, 3000, 1100, 1600]);
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
        document.MainDocumentPart!.Document.Save();
    }

    public static string VehiclePeriod(VehicleReportFilterDto filter) =>
        $"{filter.FromDate?.ToString("dd/MM/yyyy") ?? "Sin inicio"} al {filter.ToDate?.ToString("dd/MM/yyyy") ?? "Sin fin"}";
    public static string VehicleCriteria(VehicleReportFilterDto filter) =>
        $"Placa: {Value(filter.Plate)} · Propietario: {Value(filter.OwnerName)} · " +
        $"Categorías: {Choices(filter.IncludeMonthly, filter.IncludeOccasional, "Mensual", "Ocasional")} · " +
        $"Estado: {Choices(filter.IncludeInside, filter.IncludeOutside, "Dentro", "Fuera")}";
    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "Todos" : value.Trim();
    private static string Choices(bool first, bool second, string firstName, string secondName) =>
        first && second ? "Todos" : first ? firstName : second ? secondName : "Según búsqueda";

    private static void ExcelHeading(IXLWorksheet sheet, string title, int columns, DateTime generated)
    {
        sheet.ShowGridLines = false;
        sheet.Cell(1, 1).Value = "SISTEMA DE GESTIÓN DE PARQUEADERO";
        sheet.Range(1, 1, 1, columns).Merge();
        sheet.Range(1, 1, 1, columns).Style.Fill.BackgroundColor = XLColor.FromHtml("#17324D");
        sheet.Range(1, 1, 1, columns).Style.Font.FontColor = XLColor.White;
        sheet.Range(1, 1, 1, columns).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, columns).Style.Font.FontSize = 12;
        sheet.Row(1).Height = 27;
        sheet.Cell(2, 1).Value = title;
        sheet.Range(2, 1, 2, columns).Merge();
        sheet.Range(2, 1, 2, columns).Style.Font.Bold = true;
        sheet.Range(2, 1, 2, columns).Style.Font.FontSize = 18;
        sheet.Range(2, 1, 2, columns).Style.Font.FontColor = XLColor.FromHtml("#17324D");
        sheet.Row(2).Height = 34;
        sheet.Cell(3, 1).Value = $"Emitido: {generated:dd/MM/yyyy HH:mm}";
        sheet.Range(3, 1, 3, columns).Merge();
        sheet.Range(3, 1, 3, columns).Style.Font.FontColor = XLColor.FromHtml("#596B7A");
        sheet.Range(4, 1, 6, 1).Style.Font.Bold = true;
    }

    private static void ExcelHeader(IXLWorksheet sheet, int row, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++) sheet.Cell(row, i + 1).Value = titles[i];
        var range = sheet.Range(row, 1, row, titles.Length);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#245A81");
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        sheet.Row(row).Height = 32;
    }

    private static void ExcelBodyRow(IXLWorksheet sheet, int row, int columns)
    {
        var range = sheet.Range(row, 1, row, columns);
        if (row % 2 == 0) range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F8");
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(row).Height = 23;
    }

    private static void ExcelEmpty(IXLWorksheet sheet, int row, int columns)
    {
        sheet.Cell(row, 1).Value = "Sin resultados para los criterios seleccionados";
        sheet.Range(row, 1, row, columns).Merge();
        sheet.Range(row, 1, row, columns).Style.Font.FontColor = XLColor.FromHtml("#596B7A");
    }

    private static void ExcelTotal(IXLWorksheet sheet, int row, int columns)
    {
        var range = sheet.Range(row, 1, row, columns);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1F6");
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#17324D");
        range.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        range.Style.Border.TopBorderColor = XLColor.FromHtml("#245A81");
        sheet.Row(row).Height = 28;
    }

    private static void ExcelFinish(IXLWorksheet sheet, int headerRow, int lastDataRow, int columns)
    {
        sheet.Range(headerRow, 1, lastDataRow, columns).SetAutoFilter();
        sheet.SheetView.FreezeRows(headerRow);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, headerRow);
    }

    private static WordprocessingDocument NewWord(string path)
    {
        var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        return document;
    }

    private static void WordHeading(W.Body body, string title, DateTime generated)
    {
        WordText(body, "SISTEMA DE GESTIÓN DE PARQUEADERO", true, 20, Navy);
        WordText(body, title, true, 32, Navy);
        WordText(body, $"Emitido: {generated:dd/MM/yyyy HH:mm}", false, 18, Muted);
        body.Append(new W.Paragraph(new W.ParagraphProperties(
            new W.ParagraphBorders(new W.BottomBorder { Val = W.BorderValues.Single, Color = Blue, Size = 16 }),
            new W.SpacingBetweenLines { After = "160" })));
    }

    private static void WordSection(W.Body body, string title) =>
        WordText(body, title, true, 22, Blue);

    private static void WordFooter(W.Body body)
    {
        WordText(body, "Documento generado por el sistema de gestión de parqueadero.", false, 16, Muted);
        body.Append(new W.SectionProperties(
            new W.PageSize { Width = 15840, Height = 12240, Orient = W.PageOrientationValues.Landscape },
            new W.PageMargin { Top = 720, Right = 720, Bottom = 720, Left = 720 }));
    }

    private static void WordText(W.Body body, string text, bool bold = false, int size = 19, string color = Navy)
    {
        body.Append(new W.Paragraph(
            new W.ParagraphProperties(new W.SpacingBetweenLines { After = "120" }),
            new W.Run(RunStyle(bold, size, color),
                new W.Text(text) { Space = SpaceProcessingModeValues.Preserve })));
    }

    private static W.Table WordTable(params string[] headers) =>
        WordTable(headers, Enumerable.Repeat(14000 / headers.Length, headers.Length).ToArray());

    private static W.Table WordTable(IReadOnlyList<string> headers, IReadOnlyList<int> widths)
    {
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

    private static void WordRow(W.Table table, params string[] values)
    {
        var row = new W.TableRow();
        var shade = table.Elements<W.TableRow>().Count() % 2 == 0 ? Pale : "FFFFFF";
        foreach (var value in values) row.Append(WordCell(value, false, shade));
        table.Append(row);
    }

    private static void WordTotalRow(W.Table table, params string[] values)
    {
        var row = new W.TableRow();
        foreach (var value in values) row.Append(WordCell(value, true, Pale, Navy));
        table.Append(row);
    }

    private static W.TableCell WordCell(string value, bool bold, string background, string foreground = Navy)
    {
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

    private static W.RunProperties RunStyle(bool bold, int size, string color)
    {
        var style = new W.RunProperties(new W.RunFonts { Ascii = "Aptos", HighAnsi = "Aptos" });
        if (bold) style.Append(new W.Bold());
        style.Append(new W.Color { Val = color });
        style.Append(new W.FontSize { Val = size.ToString(CultureInfo.InvariantCulture) });
        return style;
    }
}
