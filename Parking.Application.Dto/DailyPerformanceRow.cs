namespace Parking.Application.Dto;

/// <summary>Indicadores observados en un día del período solicitado.</summary>
/// <param name="Date">Fecha local del parqueadero.</param>
/// <param name="Entries">Sesiones que comenzaron durante el día.</param>
/// <param name="Exits">Sesiones cerradas durante el día.</param>
/// <param name="OccupiedMinutes">Suma de minutos de ocupación registrados dentro del día.</param>
/// <param name="AvailableMinutes">Minutos observados multiplicados por la capacidad actual.</param>
/// <param name="AverageStayMinutes">Permanencia media de las sesiones que salieron ese día.</param>
/// <param name="PeakEntryHour">Hora con más ingresos; menos uno cuando no hubo entradas.</param>
/// <param name="PeakEntries">Cantidad de ingresos en la hora de mayor demanda.</param>
/// <param name="Collected">Pagos efectivamente registrados durante el día.</param>
public sealed record DailyPerformanceRow(
    DateTime Date, int Entries, int Exits, double OccupiedMinutes, double AvailableMinutes,
    int AverageStayMinutes, int PeakEntryHour, int PeakEntries, decimal Collected)
{
    /// <summary>Porcentaje de capacidad usada durante el tiempo observado.</summary>
    public double OccupancyPercent => AvailableMinutes > 0
        ? Math.Min(100, OccupiedMinutes * 100 / AvailableMinutes) : 0;

    /// <summary>Franja horaria de más ingresos para mostrar y exportar.</summary>
    public string PeakEntryLabel => PeakEntryHour < 0
        ? "Sin ingresos" : $"{PeakEntryHour:00}:00–{(PeakEntryHour + 1) % 24:00}:00 ({PeakEntries})";
}
