namespace Parking.Application.UseCases;

/// <summary>Información devuelta al operador después de intentar registrar una entrada.</summary>
/// <param name="SlotNumber">Número del puesto; nulo si no hay espacio, o EXISTENTE si ya hay una sesión abierta.</param>
/// <param name="Access">Clasificación mensual u ocasional y cuota vencida que debe mostrarse.</param>
/// <param name="Ticket">Datos del ticket ocasional; nulo si no se abrió una sesión imprimible.</param>
public sealed record EntryRegistrationResult(string? SlotNumber, MonthlyAccessDecision Access,
    EntryTicketData? Ticket = null);
