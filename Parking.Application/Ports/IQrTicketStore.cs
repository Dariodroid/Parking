using System.Threading.Tasks;

namespace Parking.Application.Ports;

/// <summary>Almacén de imágenes QR para tickets de entradas ocasionales.</summary>
public interface IQrTicketStore
{
    /// <summary>Genera y guarda el QR literal de una sesión ocasional.</summary>
    /// <param name="qrData">Identificador SESSION que leerá el escáner.</param>
    /// <param name="sessionCode">Código usado como nombre de archivo.</param>
    /// <returns>Ruta completa del PNG creado.</returns>
    Task<string> SaveAsync(string qrData, string sessionCode);

    /// <summary>Borra un PNG creado si no pudo guardarse la entrada.</summary>
    /// <param name="path">Ruta del archivo generado previamente.</param>
    void Delete(string path);
}
