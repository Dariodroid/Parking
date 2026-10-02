using Parking.Application.Dto;
using Parking.Application.Interfaces;

namespace Parking.Application.Services;

/// <summary>Reúne cobros de salidas y mensualidades sin cambiar los asientos originales.</summary>
public sealed class CashQueryService : ICashQueryService
{
    private readonly ICashRepository _payments;
    private readonly IMonthlyFeeLedgerService _monthlyLedger;

    public CashQueryService(ICashRepository payments, IMonthlyFeeLedgerService monthlyLedger)
    {
        _payments = payments;
        _monthlyLedger = monthlyLedger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CashMovement>> GetMovementsAsync(DateTime fromInclusive, DateTime toExclusive)
    {
        // Si una fuente falla, Caja no presenta una suma parcial como definitiva.
        var departures = await _payments.GetPaymentsAsync(fromInclusive, toExclusive);
        var monthly = await _monthlyLedger.GetReceiptsAsync(fromInclusive, toExclusive);
        return departures.Select(p => new CashMovement(
                p.session?.plate ?? string.Empty,
                p.collected_byNavigation?.full_name ?? string.Empty,
                p.amount_paid, p.payment_method ?? string.Empty, p.payment_reference ?? string.Empty,
                p.collected_at, p.notes ?? string.Empty, "Salida"))
            .Concat(monthly.Select(r => new CashMovement(r.Plate, r.OperatorName,
                r.Amount, r.PaymentMethod, $"MENSUAL-{r.Id}", r.CollectedAt,
                $"Cuota vencida al {r.PeriodEndDate:dd/MM/yyyy}", "Mensualidad")))
            .OrderByDescending(p => p.CollectedAt).ToList();
    }
}
