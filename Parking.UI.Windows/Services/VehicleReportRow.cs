using Parking.Application.Dto;
using System.Globalization;

namespace Parking.UI.Windows.Services;

/// <summary>Una fila legible compartida por la vista previa y el documento Word.</summary>
/// <param name="Number">Número consecutivo de la fila en el informe.</param>
/// <param name="Vehicle">Datos consultados con los filtros aplicados.</param>
public sealed record VehicleReportRow(int Number, VehicleReportDto Vehicle)
{
    private static CultureInfo CurrencyCulture => CurrencyDisplay.Culture;
    /// <summary>Tipo y categoría mostrados en dos líneas de una misma celda.</summary>
    public string TypeAndCategory => $"{Vehicle.VehicleType}\n{Vehicle.Category}";
    /// <summary>Estado y cuota del plan, o raya para una placa ocasional.</summary>
    public string PlanAndFee => Vehicle.Category == "Mensual"
        ? $"{Vehicle.PlanStatus}\n{Vehicle.MonthlyFee.ToString("C", CurrencyCulture)}"
        : "—";
    /// <summary>Conteo de ingresos y minutos acumulados en una sola celda.</summary>
    public string Activity => $"{Vehicle.TotalEntries.ToString("N0", CultureInfo.CurrentCulture)} ingresos\n" +
                              $"{Vehicle.TotalMinutesParked.ToString("N0", CultureInfo.CurrentCulture)} min";
    /// <summary>Fechas del último ingreso y la última salida.</summary>
    public string Movements => $"Ingreso: {DateText(Vehicle.LastEntryDate)}\nSalida: {DateText(Vehicle.LastExitDate)}";
    /// <summary>Total cobrado con formato monetario legible.</summary>
    public string Collected => Vehicle.TotalCollected.ToString("C", CurrencyCulture);

    /// <summary>Presenta una fecha opcional o una raya si no existe movimiento.</summary>
    /// <param name="value">Fecha opcional del movimiento.</param>
    /// <returns>Fecha y hora legibles, o raya.</returns>
    private static string DateText(DateTime? value) =>
        value?.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture) ?? "—";
}
