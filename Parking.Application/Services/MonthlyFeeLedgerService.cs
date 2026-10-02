using Parking.Application.Dto;
using Parking.Application.Interfaces;

namespace Parking.Application.Services;

/// <summary>Coordina el cobro mensual y delega su transacción al almacenamiento.</summary>
public sealed class MonthlyFeeLedgerService : IMonthlyFeeLedgerService
{
    private readonly IMonthlyFeeLedgerRepository _repository;

    public MonthlyFeeLedgerService(IMonthlyFeeLedgerRepository repository) => _repository = repository;

    public Task<IReadOnlyList<MonthlyFeeReceipt>> GetReceiptsAsync(DateTime fromInclusive, DateTime toExclusive)
        => _repository.GetReceiptsAsync(fromInclusive, toExclusive);

    /// <summary>Valida la solicitud y registra la cuota con el medio de pago normalizado.</summary>
    public Task<MonthlyFeeReceipt> RecordOverdueAsync(int planId, int operatorId, string paymentMethod)
    {
        if (planId <= 0 || operatorId <= 0)
            throw new ArgumentException("Seleccione un contrato y un operador válidos.");

        string method = paymentMethod?.Trim().ToLowerInvariant() switch
        {
            "cash" => "cash",
            "transfer" => "transfer",
            "card" => "card",
            _ => throw new ArgumentException("Seleccione efectivo, transferencia o tarjeta.")
        };

        return _repository.RecordOverdueAsync(planId, operatorId, method);
    }
}
