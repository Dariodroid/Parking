using Parking.Application.Dto;
using System.Globalization;

namespace Parking.UI.Windows.Services;

/// <summary>Una fila legible compartida por la vista previa y el documento Word.</summary>
/// <param name="Number">Número consecutivo de la fila en el informe.</param>
/// <param name="Vehicle">Datos consultados con los filtros aplicados.</param>
public sealed record VehicleReportRow(int Number, VehicleReportDto Vehicle)
{
    private static CultureInfo CurrencyCulture => CurrencyDisplay.Culture;
    /// <summary>Tipo, vínculo mensual y modalidad real de las estancias incluidas.</summary>
    public string TypeAndCategory => Vehicle.Category == "Mensual"
        ? $"{Vehicle.VehicleType}\nCliente mensual\n{Vehicle.AccessSummary}"
        : $"{Vehicle.VehicleType}\n{Vehicle.Category}";
    /// <summary>Estado efectivo del plan según sus indicadores y fechas actuales.</summary>
    public string PlanStatusLabel
    {
        get
        {
            // El estado textual por sí solo puede seguir como activo tras vencer la fecha final.
            if (Vehicle.PlanStatus == "Sin Plan") return "Sin plan";
            if (!Vehicle.VehicleIsActive || !Vehicle.PlanIsActive ||
                Vehicle.PlanStatus.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
                return "Inactivo";
            if (Vehicle.PlanStartDate?.Date > DateTime.Today) return "Aún no inicia";
            if (Vehicle.PlanEndDate?.Date < DateTime.Today) return "Vencido";
            return PlanStatusText(Vehicle.PlanStatus);
        }
    }
    /// <summary>Estado y precio del plan, diferenciados del cobro de la estancia.</summary>
    public string PlanAndFee => Vehicle.Category == "Mensual"
        ? $"Plan: {PlanStatusLabel}\nFin: {Vehicle.PlanEndDate?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? "—"}\nCuota: {Vehicle.MonthlyFee.ToString("C", CurrencyCulture)}"
        : "—";
    /// <summary>Conteo de ingresos y minutos acumulados en una sola celda.</summary>
    public string Activity => $"{Vehicle.TotalEntries.ToString("N0", CultureInfo.CurrentCulture)} ingresos\n" +
                              $"{Vehicle.TotalMinutesParked.ToString("N0", CultureInfo.CurrentCulture)} min total";
    /// <summary>Fechas del ingreso y la salida de la misma estancia más reciente.</summary>
    public string Movements => $"Ingreso: {DateText(Vehicle.LastEntryDate)}\nSalida: {DateText(Vehicle.LastExitDate)}";
    /// <summary>Total cobrado con formato monetario legible.</summary>
    public string Collected => Vehicle.TotalCollected.ToString("C", CurrencyCulture);

    /// <summary>Traduce el estado guardado del plan para la presentación del informe.</summary>
    /// <param name="status">Código de estado persistido.</param>
    /// <returns>Etiqueta legible en español.</returns>
    private static string PlanStatusText(string status) => status.ToLowerInvariant() switch
    {
        "active" => "Activo",
        "inactive" => "Inactivo",
        "cancelled" => "Cancelado",
        "expired" => "Vencido",
        _ => status
    };

    /// <summary>Presenta una fecha opcional o una raya si no existe movimiento.</summary>
    /// <param name="value">Fecha opcional del movimiento.</param>
    /// <returns>Fecha y hora legibles, o raya.</returns>
    private static string DateText(DateTime? value) =>
        value?.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture) ?? "—";
}
