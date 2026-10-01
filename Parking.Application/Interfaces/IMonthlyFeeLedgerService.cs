using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Consulta y registra los pagos de cuotas mensuales.</summary>
public interface IMonthlyFeeLedgerService
{
    /// <summary>Registra una cuota vencida en la misma transacción que actualiza el plan.</summary>
    /// <param name="planId">Contrato que recibe el pago.</param>
    /// <param name="operatorId">Operador que cobró.</param>
    /// <param name="paymentMethod">Medio de pago elegido.</param>
    /// <returns>Comprobante del cobro confirmado.</returns>
    Task<MonthlyFeeReceipt> RecordOverdueAsync(int planId, int operatorId, string paymentMethod);

    /// <summary>Obtiene los cobros mensuales del intervalo.</summary>
    /// <param name="fromInclusive">Primer instante incluido.</param>
    /// <param name="toExclusive">Primer instante excluido.</param>
    /// <returns>Comprobantes del intervalo.</returns>
    Task<IReadOnlyList<MonthlyFeeReceipt>> GetReceiptsAsync(DateTime fromInclusive, DateTime toExclusive);
}
