namespace Parking.Infrastructure.ExternalServices;

/// <summary>Lectura y regiones de un fotograma analizado por el detector de placas.</summary>
public class PlateDetectionResult
{
    /// <summary>Placa normalizada reconocida; cadena vacía si el OCR no la pudo leer.</summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>Región elegida para dibujar en el visor cuando hubo una lectura válida.</summary>
    public List<OpenCvSharp.Rect> DetectedRegions { get; set; } = new();

    /// <summary>Recorte JPEG de la placa reconocida; vacío si no hubo lectura.</summary>
    public byte[] PlateImage { get; set; } = Array.Empty<byte>();

    /// <summary>Indica que el detector encontró regiones, aunque el OCR no logró leer una placa.</summary>
    public bool HasPlateCandidates { get; set; }

    /// <summary>Verdadero cuando existe una región seleccionada para el visor.</summary>
    public bool HasDetection => DetectedRegions.Count > 0;
}
