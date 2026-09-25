using Parking.Application.EntityService;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Parking.Infrastructure.ExternalServices;

public sealed class LocalEntryPhotoStore : IEntryPhotoStore
{
    // LocalAppData permite escritura sin privilegios y evita mezclar imágenes con el ejecutable.
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Parking", "PlateCaptures");

    public async Task<string> SaveAsync(byte[] jpeg, string sessionCode)
    {
        if (jpeg == null || jpeg.Length == 0) throw new ArgumentException("La captura está vacía.", nameof(jpeg));
        DateTime now = DateTime.UtcNow;
        string directory = Path.Combine(_directory, now.ToString("yyyy"), now.ToString("MM"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{now:yyyyMMddHHmmssfff}_{sessionCode}.jpg");
        await File.WriteAllBytesAsync(path, jpeg);
        return path;
    }

    public void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
