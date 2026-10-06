using System.IO;

namespace Parking.UI.Windows.Services;

/// <summary>Lee y guarda el serial activado en el perfil de Windows.</summary>
public sealed class LicenseFileStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Parking", "license.key");

    /// <summary>Devuelve el serial guardado o null si este usuario aún no activó el sistema.</summary>
    public string? Read() => File.Exists(_path) ? File.ReadAllText(_path) : null;

    /// <summary>Reemplaza el serial de forma atómica después de validarlo.</summary>
    public void Save(string serial)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, serial.Trim());
            File.Move(temporaryPath, _path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
