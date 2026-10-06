using Parking.Infrastructure.CrossCutting.Licensing;

namespace Parking.UI.Windows.Services;

/// <summary>Exige una firma válida para esta instalación en cada arranque.</summary>
public sealed class LicenseManager
{
    private readonly InstallationIdentity _identity;
    private readonly LicenseVerifier _verifier;
    private readonly LicenseFileStore _store;

    /// <summary>Construye la comprobación sin requerir SQL ni Internet.</summary>
    public LicenseManager(InstallationIdentity identity, LicenseVerifier verifier, LicenseFileStore store)
    {
        _identity = identity;
        _verifier = verifier;
        _store = store;
    }

    /// <summary>Código que el cliente entrega al proveedor para emitir su licencia.</summary>
    public string InstallationCode => _identity.GetCode();

    /// <summary>Comprueba la licencia guardada y su correspondencia con este equipo.</summary>
    public bool TryGetLicense(out LicensePayload? license, out string error)
    {
        license = null;
        error = "Este equipo no tiene una licencia válida. Si copió el programa o la base de datos " +
            "desde otra PC, contacte al proveedor para activar esta instalación.";
        string? serial = _store.Read();
        return serial is not null && _verifier.TryVerify(serial, InstallationCode, out license, out error);
    }

    /// <summary>Guarda únicamente licencias que superan la validación de firma y equipo.</summary>
    public bool TryActivate(string token, out LicensePayload? license, out string error)
    {
        if (!_verifier.TryVerify(token, InstallationCode, out license, out error)) return false;

        _store.Save(token);
        return true;
    }
}
