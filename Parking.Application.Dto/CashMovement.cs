namespace Parking.Application.Dto;

/// <summary>Cobro visible en Caja, procedente de una salida o una cuota mensual.</summary>
/// <param name="Plate">Placa asociada al cobro.</param>
/// <param name="OperatorName">Operador que recibió el pago.</param>
/// <param name="Amount">Importe recibido con centavos exactos.</param>
/// <param name="PaymentMethod">Medio de pago registrado.</param>
/// <param name="Reference">Referencia de la transacción, si existe.</param>
/// <param name="CollectedAt">Fecha y hora del cobro.</param>
/// <param name="Notes">Detalle del cobro.</param>
/// <param name="Origin">Salida o mensualidad.</param>
public sealed record CashMovement(string Plate, string OperatorName, decimal Amount,
    string PaymentMethod, string Reference, DateTime CollectedAt, string Notes, string Origin);
