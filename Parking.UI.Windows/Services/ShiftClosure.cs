namespace Parking.UI.Windows.Services;

/// <summary>Cierre inmutable con efectivo esperado, contado y diferencia firmada.</summary>
public sealed record ShiftClosure(long Id, string Operator, DateTime StartedAt, DateTime ClosedAt,
    decimal ExpectedCash, decimal CountedCash, decimal Difference, decimal Transfer, decimal Card,
    decimal Other, string Note);
