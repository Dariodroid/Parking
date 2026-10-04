namespace Parking.Application.Dto;

/// <summary>Datos de una sesión necesarios para detectar incidencias sin exponer EF a Application.</summary>
public sealed record ControlSessionData(long Id, string Plate, string Status, DateTime EntryTime,
    DateTime? ExitTime, bool HasRegisteredVehicle, decimal? AmountDue, decimal PaidTotal, string EntryOperator,
    string ExitOperator, string? PhotoPath);
