namespace Parking.Domain.Model.Policies;

/// <summary>Resultado de comparar el efectivo entregado con el esperado.</summary>
public sealed record ShiftCloseAssessment(decimal Difference, string Note);
