using Parking.Application.EntityService;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;

namespace Parking.Infrastructure.ExternalServices;

public sealed class QrReaderService : IQrService
{
    public Task<string> ReadQrAsync(byte[] imageFrame)
    {
        return Task.Run(() =>
        {
            if (imageFrame is null || imageFrame.Length == 0)
                return string.Empty;

            try
            {
                // El detector QR nativo de OpenCV podía terminar el proceso con
                // AccessViolationException. ZXing hace la detección en código administrado.
                using var stream = new MemoryStream(imageFrame, writable: false);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                int stride = checked(bitmap.PixelWidth * 4);
                var pixels = new byte[checked(stride * bitmap.PixelHeight)];
                bitmap.CopyPixels(pixels, stride, 0);

                var reader = new BarcodeReaderGeneric
                {
                    AutoRotate = true,
                    Options =
                    {
                        PossibleFormats = new[] { BarcodeFormat.QR_CODE },
                        TryHarder = true
                    }
                };
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
