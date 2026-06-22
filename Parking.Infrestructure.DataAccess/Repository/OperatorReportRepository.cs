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

    public async Task<List<OperatorReportItem>>
        GetReportAsync(
            DateTime fromDate,
            DateTime toDate)
    {
        return await _context.payments
            .Where(x =>
                !x.is_deleted &&
                x.collected_at >= fromDate &&
                x.collected_at <= toDate)
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