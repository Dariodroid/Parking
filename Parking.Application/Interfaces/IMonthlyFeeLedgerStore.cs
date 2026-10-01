using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Persistencia transaccional de cuotas mensuales.</summary>
public interface IMonthlyFeeLedgerStore
{
    Task<MonthlyFeeReceipt> RecordOverdueAsync(int planId, int operatorId, string paymentMethod);
    Task<IReadOnlyList<MonthlyFeeReceipt>> GetReceiptsAsync(DateTime fromInclusive, DateTime toExclusive);
}
