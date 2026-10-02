using System.Data;
using Microsoft.EntityFrameworkCore;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Models;
using Parking.Domain.Model.Policies;

namespace Parking.Infrastructure.DataAccess.Repository;

/// <summary>Consulta incidencias y conserva revisiones y cierres mediante EF Core.</summary>
public sealed class OperationsControlRepository : IOperationsControlRepository
{
    private readonly IDbContextFactory<parking_dbContext> _contextFactory;

    public OperationsControlRepository(IDbContextFactory<parking_dbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>Lee sesiones, pagos, contratos y revisiones para que Domain decida las incidencias.</summary>
    public async Task<ControlIncidentData> GetIncidentDataAsync(DateTime since, DateTime today)
    {
        await using var database = await _contextFactory.CreateDbContextAsync();

        var sessions = await database.parking_sessions.AsNoTracking()
            .Where(s => !s.is_deleted &&
                ((s.status == "paid" && s.exit_time >= since) ||
                 (s.status == "active" && s.registered_vehicle_id == null)))
            .Select(s => new { s.id, s.plate, s.status, s.entry_time, s.exit_time, s.amount_due,
                s.entry_photo_path, s.registered_vehicle_id,
                EntryOperator = s.entry_operator.full_name,
                ExitOperator = s.exit_operator != null ? s.exit_operator.full_name : string.Empty })
            .ToListAsync();

        var paidIds = sessions.Where(s => s.status == "paid").Select(s => s.id).ToArray();
        var paidTotals = await database.payments.AsNoTracking()
            .Where(p => !p.is_deleted && paidIds.Contains(p.session_id))
            .GroupBy(p => p.session_id)
            .Select(g => new { SessionId = g.Key, Amount = g.Sum(p => p.amount_paid) })
            .ToDictionaryAsync(x => x.SessionId, x => x.Amount);

        var overduePlans = await database.vehicle_monthly_plans.AsNoTracking()
            .Where(p => !p.is_deleted && p.end_date.Date < today
                && !p.registered_vehicle.is_deleted)
            .Select(p => new { p.id, p.end_date, p.payment_date, p.monthly_fee,
                p.registered_vehicle.plate, p.registered_vehicle.owner_name })
            .ToListAsync();
        var reviewed = (await database.IncidentReviews.AsNoTracking()
            .Select(r => r.IncidentKey).ToListAsync()).ToHashSet();
        return new ControlIncidentData(
            sessions.Select(s => new ControlSessionData(s.id, s.plate, s.status,
                s.entry_time, s.exit_time, s.registered_vehicle_id != null,
                s.amount_due, paidTotals.GetValueOrDefault(s.id),
                s.EntryOperator, s.ExitOperator, s.entry_photo_path)).ToList(),
            overduePlans.Select(p => new ControlOverduePlanData(p.id, p.plate,
                p.owner_name, p.end_date, p.payment_date, p.monthly_fee)).ToList(), reviewed);
    }

    /// <summary>Firma una incidencia sin modificar su pago o sesión de origen.</summary>
    public async Task ReviewAsync(string key, string reason, int operatorId)
    {
        await using var database = await _contextFactory.CreateDbContextAsync();
        database.IncidentReviews.Add(new ParkingIncidentReview
        {
            IncidentKey = key,
            ReviewedBy = operatorId,
            Reason = reason
        });
        await database.SaveChangesAsync();
    }

    /// <summary>Consulta las últimas cien revisiones firmadas.</summary>
    public async Task<IReadOnlyList<IncidentReview>> GetReviewsAsync()
    {
        await using var database = await _contextFactory.CreateDbContextAsync();
        var rows = await database.IncidentReviews.AsNoTracking()
            .OrderByDescending(r => r.ReviewedAt).Take(100)
            .Select(r => new { r.IncidentKey, r.ReviewedAt, Reviewer = r.Reviewer.full_name, r.Reason })
            .ToListAsync();
        return rows.Select(r => new IncidentReview(r.IncidentKey, r.ReviewedAt, r.Reviewer, r.Reason)).ToList();
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
        return OperationsControlPolicy.DetermineShiftStart(last, end);
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
