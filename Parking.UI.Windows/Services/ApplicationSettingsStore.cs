using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Parking.UI.Windows.Services;

/// <summary>Guarda la conexión y el tema bajo el perfil Windows actual.</summary>
public sealed class ApplicationSettingsStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Parking", "application-settings.bin");

    /// <summary>Lee preferencias locales; una instalación nueva comienza sin conexión.</summary>
    /// <returns>Preferencias guardadas o valores iniciales si no hay archivo válido.</returns>
    public ApplicationSettings Load()
    {
        try
        {
            // El instalador no lleva la conexión de otra computadora.
            if (!File.Exists(_path)) return new ApplicationSettings();
            byte[] protectedData = File.ReadAllBytes(_path);
            byte[] json = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<ApplicationSettings>(json) ?? new ApplicationSettings();
        }
        catch (Exception)
        {
            // Una preferencia dañada lleva a la configuración inicial sin exponer secretos.
            return new ApplicationSettings();
        }
    }

    /// <summary>Persiste una copia cifrada de las preferencias del usuario actual.</summary>
    /// <param name="settings">Conexión y tema que se conservarán entre ejecuciones.</param>
    public void Save(ApplicationSettings settings)
    {
        // DPAPI protege también una contraseña SQL incluida en la cadena.
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(settings);
        byte[] protectedData = ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser);
        string temporaryPath = _path + ".tmp";
        File.WriteAllBytes(temporaryPath, protectedData);
        // El reemplazo evita dejar una preferencia truncada si falla la escritura.
        File.Move(temporaryPath, _path, true);
    }
}
