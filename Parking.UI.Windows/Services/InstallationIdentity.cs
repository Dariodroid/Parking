using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace Parking.UI.Windows.Services;

/// <summary>Obtiene un código estable para esta instalación de Windows, independiente del usuario.</summary>
public sealed class InstallationIdentity
{
    /// <summary>Deriva el código del identificador de Windows sin revelar ese valor al proveedor.</summary>
    public string GetCode()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        string? machineId = key?.GetValue("MachineGuid") as string;
        if (string.IsNullOrWhiteSpace(machineId))
            throw new InvalidOperationException("No se pudo identificar esta instalación de Windows.");

        byte[] value = Encoding.UTF8.GetBytes("Parking-License-Computer-v2|" +
            machineId.Trim().ToUpperInvariant());
        byte[] hash = SHA256.HashData(value);
        return Convert.ToHexString(hash.AsSpan(0, 16));
    }
}
