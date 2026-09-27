using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.Infrastructure.DataAccess;

public class OperatorReportRepository
    : IOperatorReportRepository
{
    private readonly parking_dbContext _context;

    public OperatorReportRepository(
        parking_dbContext context)
    {
        _context = context;
    }

    /// <summary>Agrupa los pagos del intervalo por operador para el informe de recaudación.</summary>
    /// <param name="fromDate">Inicio inclusivo del periodo solicitado.</param>
    /// <param name="toDate">Fin exclusivo, correspondiente al día siguiente del elegido.</param>
    /// <returns>Operadores con número de pagos e importe total, ordenados por recaudación.</returns>
    public async Task<List<OperatorReportItem>>
        GetReportAsync(
            DateTime fromDate,
            DateTime toDate)
    {
        // El límite superior exclusivo permite cubrir el día final completo.
        return await _context.payments
            .Where(x =>
                !x.is_deleted &&
                x.collected_at >= fromDate &&
                x.collected_at < toDate)
            .GroupBy(x =>
                x.collected_byNavigation.full_name)
            .Select(g =>
                new OperatorReportItem
                {
                    OperatorName = g.Key,
                    TotalPayments = g.Count(),
                    TotalAmount = g.Sum(x =>
                        x.amount_paid)
                })
            .OrderByDescending(x =>
                x.TotalAmount)
            .ToListAsync();
    }
}
