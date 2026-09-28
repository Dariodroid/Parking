using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Parking.UI.Windows.Services;

/// <summary>Guarda las fuentes de cámara cifradas para el usuario actual de Windows.</summary>
public sealed class CameraSelectionStore
{
    // DPAPI protege también las credenciales que pudieran venir dentro de una URL RTSP.
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Parking", "camera-selection.bin");

    /// <summary>Recupera las selecciones guardadas para los dos visores.</summary>
    /// <returns>Configuración descifrada, o nulo si no existe o no puede leerse.</returns>
    public CameraSelectionConfiguration? Load()
    {
        try
        {
            // La primera ejecución todavía no tiene un archivo de configuración.
            if (!File.Exists(_path)) return null;
            // DPAPI solo permite descifrar con la misma cuenta de Windows.
            byte[] protectedBytes = File.ReadAllBytes(_path);
            byte[] json = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            // La estructura JSON vuelve al registro usado por el ViewModel.
            return JsonSerializer.Deserialize<CameraSelectionConfiguration>(json);
        }
        catch (Exception)
        {
            // Una configuración corrupta no debe impedir abrir la operación.
            return null;
        }
    }

    /// <summary>Cifra y guarda las fuentes y URL elegidas por el operador.</summary>
    /// <param name="configuration">Selecciones actuales de entrada y salida.</param>
    public void Save(CameraSelectionConfiguration configuration)
    {
        // El directorio se crea al guardar por primera vez.
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        // Solo se escribe el contenido cifrado, no la URL visible en texto plano.
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(configuration);
        byte[] protectedBytes = ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }
}
