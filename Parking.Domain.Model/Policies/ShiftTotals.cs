namespace Parking.Domain.Model.Policies;

/// <summary>Totales del turno calculados por las reglas del dominio.</summary>
public sealed record ShiftTotals(decimal Cash, decimal Transfer, decimal Card, decimal Other, int PaymentCount);
