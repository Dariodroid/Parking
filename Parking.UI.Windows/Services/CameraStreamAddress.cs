namespace Parking.UI.Windows.Services;

/// <summary>Prepara una URL de vídeo y permite escribir la IP de DroidCam sin protocolo.</summary>
public static class CameraStreamAddress
{
    /// <summary>Convierte una IP o dirección abreviada de DroidCam en una URL HTTP y valida otros flujos.</summary>
    /// <param name="input">IP, IP con puerto o URL RTSP/HTTP introducida por el operador.</param>
    /// <param name="address">URL completa que se entregará a OpenCV si el valor es válido.</param>
    /// <returns>Verdadero si la dirección corresponde a un flujo de red admitido.</returns>
    public static bool TryNormalize(string? input, out string address)
    {
        // No intentamos abrir una conexión cuando el campo está vacío.
        address = string.Empty;
        string value = input?.Trim() ?? string.Empty;
        if (value.Length == 0) return false;

        // Las direcciones abreviadas se interpretan como el vídeo HTTP de DroidCam.
        bool shorthand = !value.Contains("://", StringComparison.Ordinal);
        string candidate = shorthand ? $"http://{value}" : value;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("rtsp" or "http" or "https" or "rtmp")
            || string.IsNullOrWhiteSpace(uri.Host)) return false;

        if (shorthand)
        {
            // DroidCam usa 4747 por defecto cuando solo se escribe la IP del teléfono.
            var builder = new UriBuilder(uri);
            string authority = value.Split('/', '?', '#')[0];
            bool hasPort = authority.StartsWith('[')
                ? authority.Contains("]:", StringComparison.Ordinal)
                : authority.Contains(':');
            if (!hasPort) builder.Port = 4747;
            // Su flujo de vídeo se publica en /video si no se indicó otra ruta.
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
