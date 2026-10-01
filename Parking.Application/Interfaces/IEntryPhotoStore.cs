using System.Threading.Tasks;

namespace Parking.Application.Interfaces;

/// <summary>Puerto para conservar y revertir fotografías tomadas en una entrada.</summary>
public interface IEntryPhotoStore
{
    /// <summary>Guarda la captura de la placa vinculada a una sesión.</summary>
    /// <param name="jpeg">Contenido JPEG de la captura.</param>
    /// <param name="sessionCode">Código único de la sesión.</param>
    /// <returns>Ruta de la imagen guardada.</returns>
    Task<string> SaveAsync(byte[] jpeg, string sessionCode);

    /// <summary>Elimina una captura si la operación de entrada no se pudo completar.</summary>
    /// <param name="path">Ruta de la imagen creada previamente.</param>
    void Delete(string path);
}
