namespace Parking.Application.Dto;

/// <summary>Período y totales sin clasificar para que Application prepare el cierre.</summary>
public sealed record ShiftSource(DateTime StartedAt, DateTime EndedAt,
    IReadOnlyList<ShiftPaymentTotal> Payments);
