using OpenCvSharp;
using Parking.Application.EntityService;

namespace Parking.Infrastructure.ExternalServices
{
    public class QrReaderService : IQrService
    {
        public Task<string> ReadQrAsync(byte[] imageFrame)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (imageFrame == null || imageFrame.Length == 0)
                        return string.Empty;

                    using var src = Cv2.ImDecode(imageFrame, ImreadModes.Color);
                    if (src.Empty())
                        return string.Empty;

                    using var qrDetector = new QRCodeDetector();
                    using var straightQrCode = new Mat();

                    string qrText = qrDetector.DetectAndDecode(src, out Point2f[] _, straightQrCode);
                    return qrText?.Trim() ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error procesando QR: {ex.Message}");
                    return string.Empty;
                }
            });
        }
    }
}
