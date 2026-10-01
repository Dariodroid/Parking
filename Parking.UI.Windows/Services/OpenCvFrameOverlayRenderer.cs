using Parking.UI.Windows.Interfaces;
using OpenCvSharp;
using Parking.Application.Contracts;
using Parking.Application.Interfaces;

namespace Parking.UI.Windows.Services;

/// <summary>Dibuja la guía y la lectura sobre la imagen que ve el operador.</summary>
public sealed class OpenCvFrameOverlayRenderer : IFrameOverlayRenderer
{
    private readonly IPlateService _plateService;

    /// <summary>Recibe el contrato que define la misma zona utilizada por el detector.</summary>
    /// <param name="plateService">Servicio que proporciona coordenadas independientes de OpenCV.</param>
    public OpenCvFrameOverlayRenderer(IPlateService plateService) => _plateService = plateService;

    /// <summary>Convierte las coordenadas del resultado en dibujos del visor.</summary>
    /// <param name="frameBytes">Fotograma codificado de la cámara.</param>
    /// <param name="detection">Región y placa que aún deben mostrarse.</param>
    /// <returns>Imagen JPEG con las guías.</returns>
    public byte[] Render(byte[] frameBytes, PlateDetectionResult? detection)
    {
        // La guía usa exactamente las dimensiones originales del fotograma.
        using var mat = Cv2.ImDecode(frameBytes, ImreadModes.Color);
        var roi = _plateService.GetRecognitionRegionCoordinates(mat.Width, mat.Height);
        Cv2.Rectangle(mat, new Rect(roi.X, roi.Y, roi.Width, roi.Height),
            Scalar.Cyan, 2, LineTypes.AntiAlias);

        // Solo la región elegida por el OCR recibe una caja y una etiqueta.
        if (detection?.HasDetection == true)
        {
            var region = detection.DetectedRegions[0];
            Cv2.Rectangle(mat, new Rect(region.X, region.Y, region.Width, region.Height),
                Scalar.LimeGreen, 4, LineTypes.AntiAlias);
            if (!string.IsNullOrWhiteSpace(detection.PlateNumber))
                Cv2.PutText(mat, detection.PlateNumber,
                    new Point(region.X, Math.Max(20, region.Y - 15)),
                    HersheyFonts.HersheySimplex, 1.2, Scalar.LimeGreen, 3);
        }

        // La captura original no se modifica; se devuelve una imagen nueva para WPF.
        return mat.ToBytes(".jpg");
    }
}
