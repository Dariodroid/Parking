namespace Parking.Application.Dto;

/// <summary>Total SQL de un medio de pago durante un turno.</summary>
public sealed record ShiftPaymentTotal(string? Method, decimal Amount, int Count);
