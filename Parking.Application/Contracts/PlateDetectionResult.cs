namespace Parking.Application.Contracts;

/// <summary>Datos reconocidos de un fotograma para que la interfaz los presente sin conocer OpenCV.</summary>
public sealed class PlateDetectionResult
{
    /// <summary>Placa normalizada; queda vacía cuando el OCR no pudo leerla.</summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>Regiones seleccionadas para dibujar en el visor.</summary>
    public List<PlateRegion> DetectedRegions { get; set; } = new();

    /// <summary>Recorte JPEG de la placa reconocida; vacío si no hubo lectura.</summary>
    public byte[] PlateImage { get; set; } = Array.Empty<byte>();

    /// <summary>Indica que hubo candidatos aunque el OCR no reconociera caracteres.</summary>
    public bool HasPlateCandidates { get; set; }

    /// <summary>Indica que existe al menos una región para mostrar.</summary>
    public bool HasDetection => DetectedRegions.Count > 0;
}
