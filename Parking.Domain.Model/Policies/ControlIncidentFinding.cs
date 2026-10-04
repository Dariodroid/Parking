namespace Parking.Domain.Model.Policies;

/// <summary>Decisión del dominio sobre una sesión o cuota, sin texto ni formato de UI.</summary>
public sealed record ControlIncidentFinding(ControlIncidentKind Kind, DateTime OccurredAt, decimal Difference);
