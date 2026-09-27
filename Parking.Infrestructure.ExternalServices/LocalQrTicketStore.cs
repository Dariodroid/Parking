using Parking.Application.EntityService;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.QrCode;

namespace Parking.Infrastructure.ExternalServices;

/// <summary>Genera tickets QR PNG en el perfil local del usuario.</summary>
public sealed class LocalQrTicketStore : IQrTicketStore
{
    // LocalApplicationData evita depender de una ruta fija de instalación.
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Parking", "TicketQrs");

    /// <summary>Valida el identificador, dibuja su QR y guarda el PNG del ticket.</summary>
    /// <param name="qrData">Valor SESSION literal que se codificará.</param>
    /// <param name="sessionCode">Código seguro usado para nombrar el archivo.</param>
    /// <returns>Ruta absoluta del PNG recién guardado.</returns>
    public async Task<string> SaveAsync(string qrData, string sessionCode)
    {
        // El lector espera un identificador de sesión, no un enlace web.
        if (string.IsNullOrWhiteSpace(qrData) || !qrData.StartsWith("SESSION-", StringComparison.Ordinal))
            throw new ArgumentException("El identificador de la sesión no es válido.", nameof(qrData));
        if (string.IsNullOrWhiteSpace(sessionCode) || sessionCode.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("El código de la sesión no es válido.", nameof(sessionCode));

        // Se genera en memoria antes de escribirlo en la carpeta del usuario.
        byte[] png = CreatePng(qrData);
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, $"{sessionCode}.png");
        await File.WriteAllBytesAsync(path, png);
        return path;
    }

    /// <summary>Codifica el texto de sesión en una imagen QR PNG.</summary>
    /// <param name="qrData">Contenido exacto que debe recuperar el lector.</param>
    /// <returns>Bytes PNG listos para guardar.</returns>
    private static byte[] CreatePng(string qrData)
    {
        // El QR codifica el identificador literal. Un generador de QR dinámico
        // guardaría un enlace de red y el lector no podría identificar la sesión.
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions { Width = 320, Height = 320, Margin = 4 }
        };
        // ZXing produce píxeles BGRA que WPF empaqueta como bitmap.
        var pixels = writer.Write(qrData);
        var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96,
            PixelFormats.Bgra32, null, pixels.Pixels, pixels.Width * 4);
        bitmap.Freeze();

        // El codificador PNG escribe el bitmap en un flujo de memoria.
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var image = new MemoryStream();
        encoder.Save(image);
        return image.ToArray();
    }

    /// <summary>Elimina el ticket temporal si la operación de entrada se revierte.</summary>
    /// <param name="path">Ruta devuelta previamente por <see cref="SaveAsync"/>.</param>
    public void Delete(string path)
    {
        // El archivo puede no existir si falló la escritura o ya se eliminó.
        if (File.Exists(path)) File.Delete(path);
    }
}
