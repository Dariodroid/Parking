namespace Parking.Application.Dto;

/// <summary>Datos del contrato necesarios para detectar una cuota vencida.</summary>
public sealed record ControlOverduePlanData(int Id, string Plate, string OwnerName,
    DateTime EndDate, DateTime PaymentDate, decimal MonthlyFee);
