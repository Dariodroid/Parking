using System.Data;
using Microsoft.EntityFrameworkCore;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Models;
using Parking.Domain.Model.Policies;

namespace Parking.Infrastructure.DataAccess.Repository;

/// <summary>Consulta pagos y conserva cierres de turno con EF Core.</summary>
public sealed class ShiftClosingRepository : IShiftClosingRepository
{
    private readonly IDbContextFactory<parking_dbContext> _contextFactory;

    /// <summary>Recibe la fábrica de contextos para consultas y escrituras independientes.</summary>
    public ShiftClosingRepository(IDbContextFactory<parking_dbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>Consulta pagos del turno abierto desde el último cierre o la medianoche.</summary>
    public async Task<ShiftSource> GetCurrentShiftSourceAsync(int operatorId)
    {
        await using var database = await _contextFactory.CreateDbContextAsync();
        DateTime end = DateTime.Now;
        DateTime start = await GetShiftStartAsync(database, operatorId, end);
        return await GetShiftSourceAsync(database, operatorId, start, end);
    }

    /// <summary>Conserva el cierre decidido por Application con datos leídos en la misma transacción.</summary>
    public async Task<ShiftClosure> CloseShiftAsync(int operatorId, Func<ShiftSource, ShiftClosingDecision> decide)
    {
        await using var database = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            DateTime end = DateTime.Now;
            DateTime start = await GetShiftStartAsync(database, operatorId, end);
            ShiftSource source = await GetShiftSourceAsync(database, operatorId, start, end);
            ShiftClosingDecision decision = decide(source);
            ShiftSummary totals = decision.Summary;

            var closure = new ParkingShiftClosure
            {
                OperatorId = operatorId,
                StartedAt = start,
                ClosedAt = end,
                ExpectedCash = totals.Cash,
                CountedCash = decision.CountedCash,
                Difference = decision.Difference,
                TransferTotal = totals.Transfer,
                CardTotal = totals.Card,
                OtherTotal = totals.Other,
                PaymentCount = totals.PaymentCount,
                Note = decision.Note
            };
            database.ShiftClosures.Add(closure);
            await database.SaveChangesAsync();
            await transaction.CommitAsync();
            return new ShiftClosure(closure.Id, string.Empty, start, end, totals.Cash, decision.CountedCash,
                decision.Difference, totals.Transfer, totals.Card, totals.Other, decision.Note);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>Consulta los últimos cien cierres, opcionalmente de un operador.</summary>
    public async Task<IReadOnlyList<ShiftClosure>> GetClosuresAsync(int? operatorId = null)
    {
        await using var database = await _contextFactory.CreateDbContextAsync();
        var query = database.ShiftClosures.AsNoTracking().AsQueryable();
        if (operatorId.HasValue)
            query = query.Where(c => c.OperatorId == operatorId.Value);
        var rows = await query.OrderByDescending(c => c.ClosedAt).ThenByDescending(c => c.Id)
            .Take(100)
            .Select(c => new { c.Id, Operator = c.Operator.full_name, c.StartedAt, c.ClosedAt,
                c.ExpectedCash, c.CountedCash, c.Difference, c.TransferTotal, c.CardTotal,
                c.OtherTotal, c.Note })
            .ToListAsync();
        return rows.Select(c => new ShiftClosure(c.Id, c.Operator, c.StartedAt, c.ClosedAt,
            c.ExpectedCash, c.CountedCash, c.Difference, c.TransferTotal, c.CardTotal,
            c.OtherTotal, c.Note)).ToList();
    }

    /// <summary>Busca el límite inicial del turno mientras participa en la transacción actual.</summary>
    private static async Task<DateTime> GetShiftStartAsync(parking_dbContext database, int operatorId, DateTime end)
    {
        DateTime last = await database.ShiftClosures.AsNoTracking()
            .Where(c => c.OperatorId == operatorId && c.ClosedAt <= end)
            .OrderByDescending(c => c.ClosedAt).ThenByDescending(c => c.Id)
            .Select(c => c.ClosedAt).FirstOrDefaultAsync();
        return ShiftClosingPolicy.DetermineShiftStart(last, end);
    }

    /// <summary>Agrupa pagos de salidas y mensualidades sin decidir cómo clasificar cada medio.</summary>
    private static async Task<ShiftSource> GetShiftSourceAsync(parking_dbContext database,
        int operatorId, DateTime start, DateTime end)
    {
        var departures = database.payments.AsNoTracking()
            .Where(p => !p.is_deleted && p.collected_by == operatorId
                && p.collected_at >= start && p.collected_at < end)
            .Select(p => new { Method = p.payment_method, Amount = p.amount_paid });
        var monthly = database.MonthlyFeeReceipts.AsNoTracking()
            .Where(r => r.CollectedBy == operatorId && r.CollectedAt >= start && r.CollectedAt < end)
            .Select(r => new { Method = r.PaymentMethod, Amount = r.Amount });
        var totals = await departures.Concat(monthly)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync();

        return new ShiftSource(start, end, totals.Select(row =>
            new ShiftPaymentTotal(row.Method, row.Amount, row.Count)).ToList());
    }
}
