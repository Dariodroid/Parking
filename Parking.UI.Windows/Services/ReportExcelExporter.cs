using ClosedXML.Excel;
using Parking.Application.Dto;
using Parking.Domain.Model.Models;
using System.Globalization;

namespace Parking.UI.Windows.Services;

/// <summary>Construye los informes XLSX desde los datos filtrados de la pantalla.</summary>
public sealed class ReportExcelExporter
{
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
        ReportExcelStyle.Heading(sheet, "INFORME DE OPERADORES", 3, generated);
        sheet.Cell(4, 1).Value = "PERÍODO";
        sheet.Cell(4, 2).Value = $"{from:dd/MM/yyyy} al {to:dd/MM/yyyy}";
        sheet.Cell(5, 1).Value = "OPERADORES";
        sheet.Cell(5, 2).Value = items.Count;
        sheet.Cell(5, 3).Value = $"COBROS: {items.Sum(x => x.TotalPayments):N0}";
        ReportExcelStyle.Header(sheet, 7, "Operador", "Cantidad de cobros", "Total recaudado");

        // Cada elemento ocupa una fila y recibe el estilo alternado común.
        var row = 8;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.OperatorName;
            sheet.Cell(row, 2).Value = item.TotalPayments;
            sheet.Cell(row, 3).Value = item.TotalAmount;
            ReportExcelStyle.BodyRow(sheet, row, 3);
            row++;
        }
        // El mensaje vacío y el total también se muestran sin resultados.
        if (items.Count == 0) ReportExcelStyle.Empty(sheet, row, 3);
        var totalRow = Math.Max(row, 9) + 1;
        sheet.Cell(totalRow, 1).Value = "TOTAL GENERAL";
        sheet.Cell(totalRow, 2).Value = items.Sum(x => x.TotalPayments);
        sheet.Cell(totalRow, 3).Value = items.Sum(x => x.TotalAmount);
        ReportExcelStyle.Total(sheet, totalRow, 3);
        // Los anchos y formatos hacen legibles nombres e importes en Excel.
        sheet.Column(1).Width = 38;
        sheet.Column(2).Width = 23;
        sheet.Column(3).Width = 25;
        sheet.Range(8, 3, Math.Max(row - 1, 8), 3).Style.NumberFormat.Format = CurrencyFormat;
        sheet.Cell(totalRow, 3).Style.NumberFormat.Format = CurrencyFormat;
        ReportExcelStyle.Finish(sheet, 7, Math.Max(row - 1, 7), 3);
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
        ReportExcelStyle.Heading(sheet, "INFORME DETALLADO DE VEHÍCULOS", 12, generated);
        // El encabezado documenta período, criterios y alcance del resumen.
        sheet.Cell(4, 1).Value = "PERÍODO DE BÚSQUEDA";
        sheet.Cell(4, 3).Value = VehicleReportCriteriaFormatter.VehiclePeriod(filter);
        sheet.Cell(5, 1).Value = "CRITERIOS";
        sheet.Cell(5, 3).Value = VehicleReportCriteriaFormatter.VehicleCriteria(filter);
        sheet.Cell(6, 1).Value = "RESUMEN";
        sheet.Cell(6, 3).Value = $"{items.Count:N0} vehículos · Total cobrado mostrado: {items.Sum(x => x.TotalCollected).ToString("C", Culture)}";
        sheet.Range(6, 3, 6, 12).Merge();
        sheet.Cell(7, 1).Value = "Cuota = precio del plan; cobrado = pagos de estancias. Un cliente mensual puede ingresar con tarifa ocasional fuera de cobertura.";
        sheet.Range(7, 1, 7, 12).Merge();
        sheet.Range(7, 1, 7, 12).Style.Font.FontColor = XLColor.FromHtml("#596B7A");
        ReportExcelStyle.Header(sheet, 9, "Placa", "Propietario", "Tipo", "Categoría", "Estado plan",
            "Mensualidad", "Ingresos", "Último ingreso", "Estado", "Total cobrado",
            "Minutos", "Última salida");

        // Las doce columnas conservan los datos concretos de cada resultado.
        var row = 10;
        foreach (var item in items)
        {
            WriteVehicleExcelRow(sheet, row, item);
            row++;
        }
        // La ausencia de filas se indica dentro de la misma tabla.
        if (items.Count == 0) ReportExcelStyle.Empty(sheet, row, 12);
        var totalRow = Math.Max(row, 11) + 1;
        sheet.Cell(totalRow, 1).Value = "TOTAL GENERAL";
        sheet.Cell(totalRow, 7).Value = items.Sum(x => x.TotalEntries);
        sheet.Cell(totalRow, 10).Value = items.Sum(x => x.TotalCollected);
        sheet.Cell(totalRow, 11).Value = items.Sum(x => x.TotalMinutesParked);
        ReportExcelStyle.Total(sheet, totalRow, 12);
        StyleVehicleExcelSheet(sheet, row, totalRow);
        ReportExcelStyle.Finish(sheet, 9, Math.Max(row - 1, 9), 12);
        // El libro se escribe solo después de completar todos los estilos.
        book.SaveAs(path);
    }

    /// <summary>Escribe una fila del informe de vehículos y conserva el texto del plan en dos líneas.</summary>
    private static void WriteVehicleExcelRow(IXLWorksheet sheet, int row, VehicleReportDto item)
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
        ReportExcelStyle.BodyRow(sheet, row, 12);
        if (item.Category == "Mensual")
        {
            sheet.Cell(row, 4).Style.Alignment.WrapText = true;
            sheet.Cell(row, 5).Style.Alignment.WrapText = true;
            sheet.Row(row).Height = 49;
        }
    }

    /// <summary>Aplica anchos y formatos monetarios y de fecha a la hoja de vehículos.</summary>
    private static void StyleVehicleExcelSheet(IXLWorksheet sheet, int nextRow, int totalRow)
    {
        sheet.Columns(1, 12).Width = 17;
        sheet.Column(2).Width = 30;
        sheet.Column(4).Width = 23;
        sheet.Column(5).Width = 20;
        sheet.Column(8).Width = 22;
        sheet.Column(12).Width = 22;
        sheet.Column(10).Width = 21;
        sheet.Range(10, 6, Math.Max(nextRow - 1, 10), 6).Style.NumberFormat.Format = CurrencyFormat;
        sheet.Range(10, 10, Math.Max(nextRow - 1, 10), 10).Style.NumberFormat.Format = CurrencyFormat;
        sheet.Range(10, 8, Math.Max(nextRow - 1, 10), 8).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        sheet.Range(10, 12, Math.Max(nextRow - 1, 10), 12).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        sheet.Cell(totalRow, 10).Style.NumberFormat.Format = CurrencyFormat;
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
        ReportExcelStyle.Heading(sheet, "OCUPACIÓN Y RECAUDACIÓN", 7, report.GeneratedAt);
        sheet.Cell(4, 1).Value = "PERÍODO";
        sheet.Cell(4, 2).Value = $"{from:dd/MM/yyyy} al {to:dd/MM/yyyy}";
        sheet.Cell(5, 1).Value = "CAPACIDAD ACTUAL";
        sheet.Cell(5, 2).Value = report.Capacity;
        sheet.Cell(6, 1).Value = "RESUMEN";
        sheet.Cell(6, 2).Value = $"{report.TotalEntries:N0} entradas · {report.TotalExits:N0} salidas · "
            + $"{report.OccupancyPercent:N1}% ocupación · {report.TotalCollected.ToString("C", Culture)} cobrados";
        sheet.Range(6, 2, 6, 7).Merge();
        ReportExcelStyle.Header(sheet, 8, "Día", "Entradas", "Salidas", "Ocupación", "Permanencia media",
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
            ReportExcelStyle.BodyRow(sheet, row, 7);
            row++;
        }
        if (report.Days.Count == 0) ReportExcelStyle.Empty(sheet, row, 7);
        int totalRow = Math.Max(row, 10) + 1;
        sheet.Cell(totalRow, 1).Value = "TOTAL / PROMEDIO";
        sheet.Cell(totalRow, 2).Value = report.TotalEntries;
        sheet.Cell(totalRow, 3).Value = report.TotalExits;
        sheet.Cell(totalRow, 4).Value = report.OccupancyPercent / 100;
        sheet.Cell(totalRow, 7).Value = report.TotalCollected;
        ReportExcelStyle.Total(sheet, totalRow, 7);
        sheet.Column(1).Width = 18;
        sheet.Columns(2, 3).Width = 15;
        sheet.Column(4).Width = 18;
        sheet.Column(5).Width = 23;
        sheet.Column(6).Width = 29;
        sheet.Column(7).Width = 21;
        sheet.Range(9, 1, Math.Max(row - 1, 9), 1).Style.DateFormat.Format = "dd/mm/yyyy";
        sheet.Range(9, 4, totalRow, 4).Style.NumberFormat.Format = "0.0%";
        sheet.Range(9, 7, totalRow, 7).Style.NumberFormat.Format = CurrencyFormat;
        ReportExcelStyle.Finish(sheet, 8, Math.Max(row - 1, 8), 7);
        sheet.Cell(totalRow + 2, 1).Value =
            "Actividad y ocupación: sesiones. Cobrado: únicamente payments por fecha de cobro. Se incluye cada día del período, aunque no haya pagos.";
        sheet.Range(totalRow + 2, 1, totalRow + 2, 7).Merge();
        book.SaveAs(path);
    }
}
