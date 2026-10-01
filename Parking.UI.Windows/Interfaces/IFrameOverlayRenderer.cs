using Parking.Application.Contracts;
using Parking.Application.Interfaces;

namespace Parking.UI.Windows.Interfaces;

/// <summary>Dibuja las guías visibles sobre un fotograma sin intervenir en el OCR.</summary>
public interface IFrameOverlayRenderer
{
    /// <summary>Prepara la imagen de vista previa con la zona y la placa detectada.</summary>
    /// <param name="frameBytes">Fotograma JPEG original.</param>
    /// <param name="detection">Detección visible, o nulo para mostrar solo la guía.</param>
    /// <returns>Fotograma JPEG decorado para la interfaz.</returns>
    byte[] Render(byte[] frameBytes, PlateDetectionResult? detection);
}
