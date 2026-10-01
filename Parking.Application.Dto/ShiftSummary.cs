namespace Parking.Application.Dto;

/// <summary>Totales de pagos de un operador entre la apertura y el cierre de su turno.</summary>
public sealed record ShiftSummary(DateTime StartedAt, DateTime EndedAt, decimal Cash, decimal Transfer,
    decimal Card, decimal Other, int PaymentCount)
{
    /// <summary>Suma de todos los medios de pago del turno.</summary>
    public decimal Total => Cash + Transfer + Card + Other;
}
