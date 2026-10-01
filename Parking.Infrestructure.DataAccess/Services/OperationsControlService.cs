using System.Data;
using Microsoft.EntityFrameworkCore;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess.Services;

/// <summary>Consulta incidencias y conserva revisiones y cierres mediante EF Core.</summary>
public sealed class OperationsControlService : IOperationsControlService
{
    private readonly IDbContextFactory<parking_dbContext> _contextFactory;
    private readonly ICurrencyFormatter _currencyFormatter;

    public OperationsControlService(IDbContextFactory<parking_dbContext> contextFactory,
        ICurrencyFormatter currencyFormatter)
    {
        _contextFactory = contextFactory;
        _currencyFormatter = currencyFormatter;
    }

    /// <summary>Busca diferencias de cobro, sesiones prolongadas y cuotas vencidas.</summary>
    public async Task<IReadOnlyList<ControlIncident>> GetOpenIncidentsAsync()
    {
        await using var database = await _contextFactory.CreateDbContextAsync();
        DateTime since = DateTime.Now.AddDays(-90);
        DateTime stale = DateTime.Now.AddHours(-48);

        var sessions = await database.parking_sessions.AsNoTracking()
            .Where(s => !s.is_deleted &&
                ((s.status == "paid" && s.exit_time >= since && s.amount_due > 0) ||
                 (s.status == "active" && s.entry_time < stale && s.registered_vehicle_id == null)))
            .Select(s => new { s.id, s.plate, s.status, s.entry_time, s.exit_time, s.amount_due,
                s.entry_photo_path,
                EntryOperator = s.entry_operator.full_name,
                ExitOperator = s.exit_operator != null ? s.exit_operator.full_name : string.Empty })
            .ToListAsync();

        var paidIds = sessions.Where(s => s.status == "paid").Select(s => s.id).ToArray();
        var paidTotals = await database.payments.AsNoTracking()
            .Where(p => !p.is_deleted && paidIds.Contains(p.session_id))
            .GroupBy(p => p.session_id)
            .Select(g => new { SessionId = g.Key, Amount = g.Sum(p => p.amount_paid) })
            .ToDictionaryAsync(x => x.SessionId, x => x.Amount);

        var incidents = new List<ControlIncident>();
        foreach (var session in sessions)
        {
            if (session.status == "active")
            {
                incidents.Add(new ControlIncident($"stale:{session.id}", "Sesión prolongada", session.plate,
                    session.entry_time, "Entrada sin salida registrada durante más de 48 horas.", 0m,
                    session.EntryOperator, session.entry_photo_path));
                continue;
            }

            decimal paid = paidTotals.GetValueOrDefault(session.id);
            decimal difference = (session.amount_due ?? 0m) - paid;
            if (Math.Abs(difference) >= 0.01m)
                incidents.Add(new ControlIncident($"payment:{session.id}", "Diferencia de cobro", session.plate,
                    session.exit_time ?? session.entry_time,
                    $"Debido: {_currencyFormatter.Format(session.amount_due ?? 0m)} · registrado en payments: {_currencyFormatter.Format(paid)}.",
                    difference, session.ExitOperator, session.entry_photo_path));
        }

        var overduePlans = await database.vehicle_monthly_plans.AsNoTracking()
            .Where(p => !p.is_deleted && p.end_date.Date < DateTime.Today
                && p.payment_date.Date <= p.end_date.Date && p.monthly_fee > 0
                && !p.registered_vehicle.is_deleted)
            .Select(p => new { p.id, p.end_date, p.monthly_fee,
                p.registered_vehicle.plate, p.registered_vehicle.owner_name })
            .ToListAsync();
        foreach (var plan in overduePlans)
            incidents.Add(new ControlIncident($"debt:{plan.id}", "Cuota vencida", plan.plate,
                plan.end_date, $"{plan.owner_name}: cuota pendiente del contrato vencido el {plan.end_date:dd/MM/yyyy}.",
                plan.monthly_fee, string.Empty));

        var reviewed = (await database.IncidentReviews.AsNoTracking()
            .Select(r => r.IncidentKey).ToListAsync()).ToHashSet();
        return incidents.Where(i => !reviewed.Contains(i.Key))
            .OrderByDescending(i => Math.Abs(i.Difference)).ThenBy(i => i.OccurredAt).ToList();
    }

    /// <summary>Firma una incidencia sin modificar su pago o sesión de origen.</summary>
    public async Task ReviewAsync(string key, string reason, int operatorId)
    {
        reason = reason.Trim();
        if (operatorId <= 0 || key.Length > 100 || reason.Length is < 8 or > 500)
            throw new ArgumentException("Indique un motivo de 8 a 500 caracteres.");

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

    /// <summary>Calcula el turno abierto desde el último cierre o la medianoche.</summary>
    public async Task<ShiftSummary> GetCurrentShiftAsync(int operatorId)
    {
        await using var database = await _contextFactory.CreateDbContextAsync();
        DateTime end = DateTime.Now;
        DateTime start = await GetShiftStartAsync(database, operatorId, end);
        return await CalculateShiftAsync(database, operatorId, start, end);
    }

    /// <summary>Conserva el efectivo contado y los totales del turno en una transacción.</summary>
    public async Task<ShiftClosure> CloseShiftAsync(int operatorId, decimal countedCash, string note)
    {
        if (operatorId <= 0 || countedCash < 0 || countedCash > 999999999m
            || decimal.Truncate(countedCash * 100m) != countedCash * 100m)
            throw new ArgumentException("El efectivo contado debe ser positivo y tener como máximo dos decimales.");
        note = note.Trim();
        if (note.Length > 500) throw new ArgumentException("La observación excede 500 caracteres.");

        await using var database = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            DateTime end = DateTime.Now;
            DateTime start = await GetShiftStartAsync(database, operatorId, end);
            ShiftSummary totals = await CalculateShiftAsync(database, operatorId, start, end);
            decimal difference = countedCash - totals.Cash;
            if (difference != 0m && note.Length < 8)
                throw new ArgumentException("Explique la diferencia con al menos 8 caracteres.");

            RequireExactMoney(totals.Cash);
            RequireExactMoney(totals.Transfer);
            RequireExactMoney(totals.Card);
            RequireExactMoney(totals.Other);
            RequireExactMoney(difference);

            var closure = new ParkingShiftClosure
            {
                OperatorId = operatorId,
                StartedAt = start,
                ClosedAt = end,
                ExpectedCash = totals.Cash,
                CountedCash = countedCash,
                Difference = difference,
                TransferTotal = totals.Transfer,
                CardTotal = totals.Card,
                OtherTotal = totals.Other,
                PaymentCount = totals.PaymentCount,
                Note = note
            };
            database.ShiftClosures.Add(closure);
            await database.SaveChangesAsync();
            await transaction.CommitAsync();
            return new ShiftClosure(closure.Id, string.Empty, start, end, totals.Cash, countedCash,
                difference, totals.Transfer, totals.Card, totals.Other, note);
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
        return last > end.Date ? last : end.Date;
    }

    /// <summary>Suma pagos de salidas y mensualidades sin cambiar sus asientos.</summary>
    private static async Task<ShiftSummary> CalculateShiftAsync(parking_dbContext database,
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

        decimal cash = 0, transfer = 0, card = 0, other = 0;
        int count = 0;
        foreach (var row in totals)
        {
            count += row.Count;
            switch (row.Method)
            {
                case "cash": cash += row.Amount; break;
                case "transfer": transfer += row.Amount; break;
                case "card": card += row.Amount; break;
                default: other += row.Amount; break;
            }
        }
        return new ShiftSummary(start, end, cash, transfer, card, other, count);
    }

    private static void RequireExactMoney(decimal amount)
    {
        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "El importe contiene fracciones de centavo.");
    }
}
