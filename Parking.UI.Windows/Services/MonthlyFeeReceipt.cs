namespace Parking.UI.Windows.Services;

/// <summary>Comprobante inmutable de una cuota mensual cobrada expresamente.</summary>
/// <param name="Id">Identificador del asiento mensual.</param>
/// <param name="PlanId">Contrato al que corresponde la cuota.</param>
/// <param name="Plate">Placa conservada al cobrar.</param>
/// <param name="OperatorName">Usuario que recibió el dinero.</param>
/// <param name="Amount">Importe exacto de la cuota.</param>
/// <param name="PaymentMethod">Medio de pago declarado.</param>
/// <param name="CollectedAt">Momento del cobro.</param>
/// <param name="PeriodEndDate">Fecha de vencimiento de la cuota saldada.</param>
public sealed record MonthlyFeeReceipt(long Id, int PlanId, string Plate, string OperatorName,
    decimal Amount, string PaymentMethod, DateTime CollectedAt, DateTime PeriodEndDate);
