namespace Parking.UI.Windows.Services;

/// <summary>Fila de Caja que reúne cobros de salidas y cuotas mensuales sin cambiar sus asientos originales.</summary>
/// <param name="Plate">Vehículo al que corresponde el cobro.</param>
/// <param name="OperatorName">Usuario que lo registró.</param>
/// <param name="Amount">Cantidad recibida, con centavos exactos.</param>
/// <param name="PaymentMethod">Efectivo, transferencia, tarjeta u otro.</param>
/// <param name="Reference">Referencia opcional del pago.</param>
/// <param name="CollectedAt">Fecha y hora del cobro.</param>
/// <param name="Notes">Detalle visible en Caja.</param>
/// <param name="Origin">Salida o mensualidad.</param>
public sealed record CashMovement(string Plate, string OperatorName, decimal Amount,
    string PaymentMethod, string Reference, DateTime CollectedAt, string Notes, string Origin);
