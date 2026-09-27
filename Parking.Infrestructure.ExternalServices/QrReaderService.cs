using Parking.Application.EntityService;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;

namespace Parking.Infrastructure.ExternalServices;

/// <summary>Lee tickets QR directamente desde fotogramas de la cámara.</summary>
public sealed class QrReaderService : IQrService
{
    /// <summary>Busca un QR de sesión en una imagen sin bloquear el hilo visual.</summary>
    /// <param name="imageFrame">Fotograma codificado, normalmente JPEG.</param>
    /// <returns>Texto leído y recortado, o cadena vacía si no hay QR válido.</returns>
    public Task<string> ReadQrAsync(byte[] imageFrame)
    {
        // La decodificación de imagen y QR se traslada a un hilo de trabajo.
        return Task.Run(() =>
        {
            // No se intenta decodificar un fotograma ausente.
            if (imageFrame is null || imageFrame.Length == 0)
                return string.Empty;

            try
            {
                // El detector QR nativo de OpenCV podía terminar el proceso con
                // AccessViolationException. ZXing hace la detección en código administrado.
                // WPF convierte el fotograma a BGRA32, formato que ZXing recibe.
                using var stream = new MemoryStream(imageFrame, writable: false);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                int stride = checked(bitmap.PixelWidth * 4);
                var pixels = new byte[checked(stride * bitmap.PixelHeight)];
                bitmap.CopyPixels(pixels, stride, 0);

                // El lector limita la búsqueda a QR y permite rotación.
                var reader = new BarcodeReaderGeneric
                {
                    AutoRotate = true,
                    Options =
                    {
                        PossibleFormats = new[] { BarcodeFormat.QR_CODE },
                        TryHarder = true
                    }
                };
                // Sin resultado se devuelve vacío para que el visor continúe.
                return reader.Decode(pixels, bitmap.PixelWidth, bitmap.PixelHeight,
                    RGBLuminanceSource.BitmapFormat.BGRA32)?.Text?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando QR: {ex.Message}");
                return string.Empty;
            }
        });
    }
}
