using System.Data;
using Microsoft.EntityFrameworkCore;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Application.UseCases;
using Parking.Domain.Model.Models;
using Parking.Domain.Model.Policies;

namespace Parking.Infrastructure.DataAccess.Services;

/// <summary>Guarda los cobros mensuales con EF Core y confirma el recibo y el contrato juntos.</summary>
public sealed class MonthlyFeeLedgerStore : IMonthlyFeeLedgerStore
{
    private readonly IDbContextFactory<parking_dbContext> _contextFactory;

    public MonthlyFeeLedgerStore(IDbContextFactory<parking_dbContext> contextFactory)
        => _contextFactory = contextFactory;

    /// <summary>Registra una cuota vencida sin modificar las fechas de vigencia del contrato.</summary>
    public async Task<MonthlyFeeReceipt> RecordOverdueAsync(int planId, int operatorId, string paymentMethod)
    {
        await using var database = await _contextFactory.CreateDbContextAsync();
        // La lectura, el recibo y la marca de pago pertenecen a una sola transacción.
        // La clave única (plan, período) impide un segundo asiento incluso entre equipos.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var plan = await database.vehicle_monthly_plans
                .Include(p => p.registered_vehicle)
                .SingleOrDefaultAsync(p => p.id == planId && !p.is_deleted && !p.registered_vehicle.is_deleted);
            string? operatorName = await database.users
                .Where(u => u.id == operatorId)
                .Select(u => u.full_name)
                .SingleOrDefaultAsync();

            if (plan is null || operatorName is null)
                throw new InvalidOperationException("No se encontró el contrato mensual o el operador.");

            DateTime collectedAt = DateTime.Now;
            MonthlyFeePaymentPolicy.RequirePendingPeriod(collectedAt, plan.end_date, plan.payment_date);
            MoneyAmount.RequireValid(plan.monthly_fee, "La cuota mensual");
            MonthlyFeePaymentPolicy.RequirePositiveAmount(plan.monthly_fee);

            var receipt = new ParkingMonthlyFeeReceipt
            {
                PlanId = planId,
                Plate = plan.registered_vehicle.plate,
                Amount = plan.monthly_fee,
                PaymentMethod = paymentMethod,
                CollectedBy = operatorId,
                CollectedAt = collectedAt,
                PeriodEndDate = plan.end_date.Date
            };
            database.MonthlyFeeReceipts.Add(receipt);

            plan.payment_date = collectedAt;
            plan.collected_by = operatorId;
            plan.updated_at = collectedAt;
            plan.updated_by = operatorId;

            await database.SaveChangesAsync();
            await transaction.CommitAsync();

            return new MonthlyFeeReceipt(receipt.Id, planId, receipt.Plate, operatorName,
                receipt.Amount, paymentMethod, collectedAt, receipt.PeriodEndDate);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>Obtiene únicamente los recibos confirmados en el período solicitado.</summary>
    public async Task<IReadOnlyList<MonthlyFeeReceipt>> GetReceiptsAsync(DateTime fromInclusive, DateTime toExclusive)
    {
        await using var database = await _contextFactory.CreateDbContextAsync();
        var rows = await database.MonthlyFeeReceipts.AsNoTracking()
            .Where(r => r.CollectedAt >= fromInclusive && r.CollectedAt < toExclusive)
            .OrderByDescending(r => r.CollectedAt)
            .ThenByDescending(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.PlanId,
                r.Plate,
                OperatorName = r.Collector.full_name,
                r.Amount,
                r.PaymentMethod,
                r.CollectedAt,
                r.PeriodEndDate
            })
            .ToListAsync();

        return rows.Select(r => new MonthlyFeeReceipt(r.Id, r.PlanId, r.Plate, r.OperatorName,
            r.Amount, r.PaymentMethod, r.CollectedAt, r.PeriodEndDate)).ToList();
    }
}
