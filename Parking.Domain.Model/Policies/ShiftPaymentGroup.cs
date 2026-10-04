namespace Parking.Domain.Model.Policies;

/// <summary>Pagos ya agrupados por medio, independientes de EF y de la base de datos.</summary>
public sealed record ShiftPaymentGroup(string? Method, decimal Amount, int Count);
