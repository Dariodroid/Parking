using Microsoft.EntityFrameworkCore;
using Parking.Application.Dto;
using Parking.Application.Interfaces;

namespace Parking.Infrastructure.DataAccess.Repository;

/// <summary>Calcula actividad desde sesiones y recaudación exclusivamente desde pagos.</summary>
public sealed class ParkingPerformanceRepository : IParkingPerformanceRepository
{
    private readonly parking_dbContext _context;

    /// <summary>Recibe el contexto de la instalación activa.</summary>
    /// <param name="context">Base de datos de sesiones, puestos y pagos.</param>
    public ParkingPerformanceRepository(parking_dbContext context) => _context = context;

    /// <summary>Agrupa por día entradas, salidas, ocupación temporal y pagos registrados.</summary>
    /// <param name="fromInclusive">Primer día incluido.</param>
    /// <param name="toExclusive">Día siguiente al último incluido.</param>
    /// <param name="observedAt">Momento de emisión que limita el día actual.</param>
    /// <returns>Filas diarias y capacidad actual.</returns>
    public async Task<ParkingPerformanceReport> GetAsync(
        DateTime fromInclusive, DateTime toExclusive, DateTime observedAt)
    {
        // Los puestos actuales sirven de denominador; el sistema no guarda cambios históricos de capacidad.
        int capacity = await _context.parking_slots.AsNoTracking().CountAsync();
        // Se incluyen sesiones iniciadas antes del período si aún ocupaban un puesto al comenzar.
        var sessions = await _context.parking_sessions.AsNoTracking()
            .Where(s => !s.is_deleted && s.entry_time < toExclusive
                && (s.exit_time == null || s.exit_time > fromInclusive))
            .Select(s => new { s.entry_time, s.exit_time })
            .ToListAsync();
        // La recaudación se atribuye al día del cobro, no al día de entrada del vehículo.
        DateTime paymentEnd = toExclusive < observedAt ? toExclusive : observedAt;
        var payments = await _context.payments.AsNoTracking()
            .Where(p => !p.is_deleted && p.collected_at >= fromInclusive
                && p.collected_at < paymentEnd)
            .Select(p => new { p.collected_at, p.amount_paid })
            .ToListAsync();
        var collectedByDay = payments.GroupBy(p => p.collected_at.Date)
            .ToDictionary(group => group.Key, group => group.Sum(p => p.amount_paid));

        var days = new List<DailyPerformanceRow>();
        for (DateTime day = fromInclusive.Date; day < toExclusive.Date; day = day.AddDays(1))
        {
            // El día actual usa solo los minutos ya transcurridos; los días futuros quedan en cero.
            DateTime dayEnd = day.AddDays(1);
            DateTime observedEnd = observedAt < dayEnd ? observedAt : dayEnd;
            if (observedEnd < day) observedEnd = day;
            var entries = sessions.Where(s => s.entry_time >= day && s.entry_time < observedEnd).ToList();
            var exits = sessions.Where(s => s.exit_time >= day && s.exit_time < observedEnd).ToList();
            // Cada sesión aporta únicamente la intersección con este día, incluso si cruzó medianoche.
            double occupiedMinutes = sessions.Sum(s =>
            {
                DateTime start = s.entry_time > day ? s.entry_time : day;
                DateTime end = s.exit_time ?? observedAt;
                if (end > observedEnd) end = observedEnd;
                return Math.Max(0, (end - start).TotalMinutes);
            });
            int averageStay = exits.Count == 0 ? 0 : (int)Math.Round(exits.Average(s =>
                Math.Max(0, (s.exit_time!.Value - s.entry_time).TotalMinutes)));
            var peak = entries.GroupBy(s => s.entry_time.Hour)
                .OrderByDescending(group => group.Count()).ThenBy(group => group.Key).FirstOrDefault();
            collectedByDay.TryGetValue(day, out decimal collected);
            days.Add(new DailyPerformanceRow(day, entries.Count, exits.Count, occupiedMinutes,
                capacity * (observedEnd - day).TotalMinutes, averageStay,
                peak?.Key ?? -1, peak?.Count() ?? 0, collected));
        }
        return new ParkingPerformanceReport(capacity, days, observedAt);
    }
}
