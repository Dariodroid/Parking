namespace Parking.Application.Dto;

/// <summary>Situación operativa que requiere revisión sin modificar la sesión ni el pago original.</summary>
public sealed record ControlIncident(string Key, string Category, string Plate, DateTime OccurredAt,
    string Detail, decimal Difference, string Operator, string? PhotoPath = null);
