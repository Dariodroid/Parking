using Microsoft.EntityFrameworkCore;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess.Repository;

/// <summary>Consulta incidencias y conserva revisiones con EF Core.</summary>
public sealed class ControlIncidentRepository : IControlIncidentRepository
{
    private readonly IDbContextFactory<parking_dbContext> _contextFactory;

    /// <summary>Recibe la fábrica de contextos para consultas y escrituras independientes.</summary>
    public ControlIncidentRepository(IDbContextFactory<parking_dbContext> contextFactory)
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
}
