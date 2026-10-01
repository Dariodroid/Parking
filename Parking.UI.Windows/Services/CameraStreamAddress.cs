using Parking.Application.Contracts;
using Parking.Application.Interfaces;

namespace Parking.UI.Windows.Services;

/// <summary>Valida URLs de vídeo genéricas y ofrece una abreviatura opcional para DroidCam.</summary>
public static class CameraStreamAddress
{
    /// <summary>Valida una URL de cámara o completa la IP de DroidCam cuando se eligió ese perfil.</summary>
    /// <param name="input">Dirección introducida por el operador.</param>
    /// <param name="profile">Perfil genérico o abreviatura opcional de DroidCam.</param>
    /// <param name="address">URL completa que se entregará a OpenCV si el valor es válido.</param>
    /// <returns>Verdadero si la dirección corresponde a un flujo de red admitido.</returns>
    public static bool TryNormalize(string? input, CameraAddressProfile profile, out string address)
    {
        // No intentamos abrir una conexión cuando el campo está vacío.
        address = string.Empty;
        string value = input?.Trim() ?? string.Empty;
        if (value.Length == 0) return false;

        // Solo el perfil específico de DroidCam acepta una IP sin protocolo.
        bool shorthand = profile == CameraAddressProfile.DroidCam
            && !value.Contains("://", StringComparison.Ordinal);
        string candidate = shorthand ? $"http://{value}" : value;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("rtsp" or "http" or "https" or "rtmp")
            || string.IsNullOrWhiteSpace(uri.Host)) return false;

        if (shorthand)
        {
            // El perfil opcional usa 4747 cuando solo se escribe la IP del teléfono.
            var builder = new UriBuilder(uri);
            string authority = value.Split('/', '?', '#')[0];
            bool hasPort = authority.StartsWith('[')
                ? authority.Contains("]:", StringComparison.Ordinal)
                : authority.Contains(':');
            if (!hasPort) builder.Port = 4747;
            // El flujo DroidCam se publica en /video si no se indicó otra ruta.
            if (builder.Path is "" or "/") builder.Path = "/video";
            address = builder.Uri.AbsoluteUri;
        }
        else
        {
            // Una URL completa puede pertenecer a cualquier cámara IP compatible.
            address = uri.AbsoluteUri;
        }
        return true;
    }
}
