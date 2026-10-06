using Parking.Application.Dto;
using Parking.Domain.Model.Models;
using System.Globalization;

namespace Parking.UI.Windows.Services;

/// <summary>Construye los informes DOCX desde los datos filtrados de la pantalla.</summary>
public sealed class ReportWordExporter
{
    private static CultureInfo Culture => CurrencyDisplay.Culture;

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
        using var document = ReportWordLayout.NewWord(path);
        var body = ReportWordLayout.GetBody(document);
        ReportWordLayout.WordHeading(body, "INFORME DE OPERADORES", generated);
        ReportWordLayout.WordText(body, $"Período: {from:dd/MM/yyyy} al {to:dd/MM/yyyy}", true);
        ReportWordLayout.WordText(body, $"{items.Count:N0} operadores  |  {items.Sum(x => x.TotalPayments):N0} cobros  |  Total recaudado: {items.Sum(x => x.TotalAmount).ToString("C", Culture)}", true);
        // La tabla contiene una fila por operador y un total al final.
        var table = ReportWordLayout.WordTable("Operador", "Cantidad de cobros", "Total recaudado");
        foreach (var item in items)
            ReportWordLayout.WordRow(table, item.OperatorName, item.TotalPayments.ToString("N0", Culture),
                item.TotalAmount.ToString("C", Culture));
        if (items.Count == 0) ReportWordLayout.WordRow(table, "Sin resultados", "", "");
        ReportWordLayout.WordTotalRow(table, "TOTAL GENERAL", items.Sum(x => x.TotalPayments).ToString("N0", Culture),
            items.Sum(x => x.TotalAmount).ToString("C", Culture));
        body.Append(table);
        ReportWordLayout.WordFooter(body);
        // Se confirma la estructura OpenXML antes de cerrar el archivo.
        ReportWordLayout.Save(document);
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
        using var document = ReportWordLayout.NewWord(path);
        var body = ReportWordLayout.GetBody(document);
        ReportWordLayout.WordHeading(body, "INFORME DETALLADO DE VEHÍCULOS", generated);
        ReportWordLayout.WordText(body, $"Período de búsqueda: {VehicleReportCriteriaFormatter.VehiclePeriod(filter)}", true);
        ReportWordLayout.WordText(body, $"Criterios: {VehicleReportCriteriaFormatter.VehicleCriteria(filter)}");
        ReportWordLayout.WordText(body, $"{items.Count:N0} vehículos  |  Total cobrado mostrado: {items.Sum(x => x.TotalCollected).ToString("C", Culture)}", true);
        ReportWordLayout.WordText(body, "Cuota = precio del plan; cobrado = pagos de estancias. Un cliente mensual puede ingresar con tarifa ocasional fuera de cobertura.");

        ReportWordLayout.WordSection(body, "VEHÍCULOS ENCONTRADOS");
        // Los anchos explícitos mantienen una sola tabla legible en paisaje.
        var table = ReportWordLayout.WordTable(
            ["Placa", "Propietario", "Tipo / categoría", "Plan / cuota",
             "Ingresos / min", "Últimos movimientos", "Estado", "Cobrado"],
            [1200, 2300, 1800, 1500, 1500, 3000, 1100, 1600]);
        // VehicleReportRow reúne los textos que comparte Word con la vista.
        for (var i = 0; i < items.Count; i++)
        {
            var row = new VehicleReportRow(i + 1, items[i]);
            ReportWordLayout.WordRow(table, row.Vehicle.Plate, row.Vehicle.OwnerName, row.TypeAndCategory,
                row.PlanAndFee, row.Activity, row.Movements, row.Vehicle.CurrentStatus, row.Collected);
        }
        if (items.Count == 0) ReportWordLayout.WordRow(table, "Sin resultados", "", "", "", "", "", "", "");
        ReportWordLayout.WordTotalRow(table, "TOTAL", $"{items.Count:N0} vehículos", "", "",
            $"{items.Sum(x => x.TotalEntries):N0} ingresos\n{items.Sum(x => x.TotalMinutesParked):N0} min",
            "", "", items.Sum(x => x.TotalCollected).ToString("C", Culture));
        body.Append(table);
        ReportWordLayout.WordFooter(body);
        // La tabla completa y el pie se persisten como documento OpenXML.
        ReportWordLayout.Save(document);
    }

    /// <summary>Genera un documento Word con el mismo resumen y detalle diario de la pantalla.</summary>
    /// <param name="path">Destino DOCX.</param>
    /// <param name="report">Instantánea aplicada.</param>
    /// <param name="from">Primer día aplicado.</param>
    /// <param name="to">Último día aplicado.</param>
    public void ExportPerformanceWord(string path, ParkingPerformanceReport report, DateTime from, DateTime to)
    {
        using var document = ReportWordLayout.NewWord(path);
        var body = ReportWordLayout.GetBody(document);
        ReportWordLayout.WordHeading(body, "OCUPACIÓN Y RECAUDACIÓN", report.GeneratedAt);
        ReportWordLayout.WordText(body, $"Período: {from:dd/MM/yyyy} al {to:dd/MM/yyyy}  |  Capacidad actual: {report.Capacity}", true);
        ReportWordLayout.WordText(body, $"{report.TotalEntries:N0} entradas  |  {report.TotalExits:N0} salidas  |  "
            + $"{report.OccupancyPercent:N1}% ocupación  |  {report.TotalCollected.ToString("C", Culture)} cobrados", true);
        ReportWordLayout.WordText(body, "Actividad y ocupación: sesiones. Cobrado: únicamente payments por fecha de cobro. Se incluye cada día del período, aunque no haya pagos.");
        ReportWordLayout.WordSection(body, "EVOLUCIÓN DIARIA");
        var table = ReportWordLayout.WordTable("Día", "Entradas", "Salidas", "Ocupación",
            "Estancia media", "Hora pico", "Cobrado");
        foreach (var day in report.Days)
            ReportWordLayout.WordRow(table, day.Date.ToString("dd/MM/yyyy"), day.Entries.ToString("N0", Culture),
                day.Exits.ToString("N0", Culture), $"{day.OccupancyPercent:N1}%",
                $"{day.AverageStayMinutes:N0} min", day.PeakEntryLabel, day.Collected.ToString("C", Culture));
        if (report.Days.Count == 0) ReportWordLayout.WordRow(table, "Sin resultados", "", "", "", "", "", "");
        ReportWordLayout.WordTotalRow(table, "TOTAL / PROM.", report.TotalEntries.ToString("N0", Culture),
            report.TotalExits.ToString("N0", Culture), $"{report.OccupancyPercent:N1}%", "", "",
            report.TotalCollected.ToString("C", Culture));
        body.Append(table);
        ReportWordLayout.WordFooter(body);
        ReportWordLayout.Save(document);
    }
}
