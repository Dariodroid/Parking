namespace Parking.Application.Dto;

/// <summary>Datos de una sesión necesarios para detectar incidencias sin exponer EF a Application.</summary>
public sealed record ControlSessionData(long Id, string Plate, string Status, DateTime EntryTime,
    DateTime? ExitTime, bool HasRegisteredVehicle, decimal? AmountDue, decimal PaidTotal, string EntryOperator,
    string ExitOperator, string? PhotoPath);

/// <summary>Datos del contrato necesarios para detectar una cuota vencida.</summary>
public sealed record ControlOverduePlanData(int Id, string Plate, string OwnerName,
    DateTime EndDate, DateTime PaymentDate, decimal MonthlyFee);

/// <summary>Lecturas del centro de control obtenidas en DataAccess.</summary>
public sealed record ControlIncidentData(IReadOnlyList<ControlSessionData> Sessions,
    IReadOnlyList<ControlOverduePlanData> OverduePlans, IReadOnlySet<string> ReviewedKeys);

/// <summary>Total SQL de un medio de pago durante un turno.</summary>
public sealed record ShiftPaymentTotal(string? Method, decimal Amount, int Count);

/// <summary>Período y totales sin clasificar para que Application prepare el cierre.</summary>
public sealed record ShiftSource(DateTime StartedAt, DateTime EndedAt,
    IReadOnlyList<ShiftPaymentTotal> Payments);

/// <summary>Resultado validado por Application que DataAccess conserva en la misma transacción.</summary>
public sealed record ShiftClosingDecision(ShiftSummary Summary, decimal CountedCash,
    decimal Difference, string Note);
