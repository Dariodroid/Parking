using Parking.Application.Dto;
using System.Globalization;

namespace Parking.UI.Windows.Services;

/// <summary>Una fila legible compartida por la vista previa y el documento Word.</summary>
public sealed record VehicleReportRow(int Number, VehicleReportDto Vehicle)
{
    private static readonly CultureInfo CurrencyCulture = CultureInfo.GetCultureInfo("en-US");
    public string TypeAndCategory => $"{Vehicle.VehicleType}\n{Vehicle.Category}";
    public string PlanAndFee => Vehicle.Category == "Mensual"
        ? $"{Vehicle.PlanStatus}\n{Vehicle.MonthlyFee.ToString("C", CurrencyCulture)}"
        : "—";
    public string Activity => $"{Vehicle.TotalEntries.ToString("N0", CultureInfo.CurrentCulture)} ingresos\n" +
                              $"{Vehicle.TotalMinutesParked.ToString("N0", CultureInfo.CurrentCulture)} min";
    public string Movements => $"Ingreso: {DateText(Vehicle.LastEntryDate)}\nSalida: {DateText(Vehicle.LastExitDate)}";
    public string Collected => Vehicle.TotalCollected.ToString("C", CurrencyCulture);

    private static string DateText(DateTime? value) =>
        value?.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture) ?? "—";
}
