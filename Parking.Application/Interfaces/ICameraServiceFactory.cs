namespace Parking.Application.Interfaces;

/// <summary>Crea una captura independiente para cada visor simultáneo.</summary>
public interface ICameraServiceFactory
{
    /// <summary>Construye un servicio de captura que posee y libera su propio dispositivo.</summary>
    /// <returns>Una captura nueva, inicialmente detenida.</returns>
    ICameraService Create();
}
