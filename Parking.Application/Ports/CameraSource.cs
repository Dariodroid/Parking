namespace Parking.Application.Ports;

/// <summary>Fuente seleccionable para uno de los visores de operación.</summary>
/// <param name="Id">Identificador estable durante la ejecución para comparar selecciones.</param>
/// <param name="DisplayName">Nombre presentado en la lista de cámaras.</param>
/// <param name="Kind">Indica si se abre un dispositivo local o un flujo de red.</param>
/// <param name="DeviceIndex">Índice OpenCV de Windows; solo se usa en dispositivos locales.</param>
/// <param name="StreamUrl">Dirección del flujo; solo se usa para abrir una cámara de red.</param>
/// <param name="AddressProfile">Regla para completar una dirección de red abreviada.</param>
public sealed record CameraSource(
    string Id,
    string DisplayName,
    CameraSourceKind Kind,
    int? DeviceIndex = null,
    string? StreamUrl = null,
    CameraAddressProfile AddressProfile = CameraAddressProfile.Generic);
