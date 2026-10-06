namespace Parking.Infrastructure.CrossCutting.Licensing;

/// <summary>Datos que el proveedor firma para una instalación concreta.</summary>
public sealed record LicensePayload(
    int Version,
    string LicenseId,
    string Customer,
    string InstallationCode,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc = null);
