using Parking.Application.EntityService;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.QrCode;

namespace Parking.Infrastructure.ExternalServices;

public sealed class LocalQrTicketStore : IQrTicketStore
{
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Parking", "TicketQrs");

    public async Task<string> SaveAsync(string qrData, string sessionCode)
    {
        if (string.IsNullOrWhiteSpace(qrData) || !qrData.StartsWith("SESSION-", StringComparison.Ordinal))
            throw new ArgumentException("El identificador de la sesión no es válido.", nameof(qrData));
        if (string.IsNullOrWhiteSpace(sessionCode) || sessionCode.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("El código de la sesión no es válido.", nameof(sessionCode));

        byte[] png = CreatePng(qrData);
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, $"{sessionCode}.png");
        await File.WriteAllBytesAsync(path, png);
        return path;
    }

    private static byte[] CreatePng(string qrData)
    {
        // El QR codifica el identificador literal. Un generador de QR dinámico
        // guardaría un enlace de red y el lector no podría identificar la sesión.
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions { Width = 320, Height = 320, Margin = 4 }
        };
        var pixels = writer.Write(qrData);
        var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96,
            PixelFormats.Bgra32, null, pixels.Pixels, pixels.Width * 4);
        bitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var image = new MemoryStream();
        encoder.Save(image);
        return image.ToArray();
    }

    public void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
