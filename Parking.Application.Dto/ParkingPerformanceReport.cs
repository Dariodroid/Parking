namespace Parking.Application.Dto;

/// <summary>Instantánea de rendimiento operativo para el período aplicado.</summary>
/// <param name="Capacity">Cantidad actual de puestos usada como base de ocupación.</param>
/// <param name="Days">Filas diarias del período solicitado.</param>
/// <param name="GeneratedAt">Instante hasta el que se observó el día en curso.</param>
public sealed record ParkingPerformanceReport(
    int Capacity, IReadOnlyList<DailyPerformanceRow> Days, DateTime GeneratedAt)
{
    /// <summary>Ingresos iniciados en todo el período.</summary>
    public int TotalEntries => Days.Sum(day => day.Entries);
    /// <summary>Salidas registradas en todo el período.</summary>
    public int TotalExits => Days.Sum(day => day.Exits);
    /// <summary>Importe de pagos recibidos en todo el período.</summary>
    public decimal TotalCollected => Days.Sum(day => day.Collected);
    /// <summary>Ocupación ponderada por la duración observada de cada día.</summary>
    public double OccupancyPercent => Days.Sum(day => day.AvailableMinutes) > 0
        ? Math.Min(100, Days.Sum(day => day.OccupiedMinutes) * 100
            / Days.Sum(day => day.AvailableMinutes)) : 0;
}
