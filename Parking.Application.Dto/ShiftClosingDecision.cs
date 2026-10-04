namespace Parking.Application.Dto;

/// <summary>Resultado validado por Application que DataAccess conserva en la misma transacción.</summary>
public sealed record ShiftClosingDecision(ShiftSummary Summary, decimal CountedCash,
    decimal Difference, string Note);
