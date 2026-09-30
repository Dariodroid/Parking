namespace Parking.Application.UseCases;

/// <summary>Datos confirmados de una entrada ocasional para impresión o reimpresión.</summary>
/// <param name="SessionCode">Código interno de la sesión guardada.</param>
/// <param name="Plate">Placa que figura en la sesión.</param>
/// <param name="SlotNumber">Puesto asignado al vehículo.</param>
/// <param name="EntryTime">Fecha y hora local de entrada guardada.</param>
/// <param name="QrImagePath">PNG que codifica el identificador SESSION de esta estancia.</param>
public sealed record EntryTicketData(string SessionCode, string Plate, string SlotNumber,
    DateTime EntryTime, string QrImagePath);
