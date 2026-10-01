using Parking.Application.Interfaces;

namespace Parking.UI.Windows.Services;

/// <summary>Proporciona a los adaptadores SQL la conexión protegida en las preferencias locales.</summary>
public sealed class ApplicationConnectionStringProvider : IConnectionStringProvider
{
    private readonly ApplicationSettingsStore _settings;

    /// <summary>Conserva el almacén que lee la configuración del equipo.</summary>
    /// <param name="settings">Preferencias cifradas del usuario Windows actual.</param>
    public ApplicationConnectionStringProvider(ApplicationSettingsStore settings) => _settings = settings;

    /// <summary>Devuelve la conexión vigente sin copiarla en el proyecto de datos.</summary>
    /// <returns>Cadena SQL guardada para esta instalación.</returns>
    public string GetConnectionString() => _settings.Load().ConnectionString;
}
