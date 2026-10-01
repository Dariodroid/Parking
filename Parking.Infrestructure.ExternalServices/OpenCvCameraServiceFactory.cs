using Parking.Application.Ports;

namespace Parking.Infrastructure.ExternalServices;

/// <summary>Entrega capturas OpenCV independientes para operar varios visores.</summary>
public sealed class OpenCvCameraServiceFactory : ICameraServiceFactory
{
    /// <summary>Crea una captura sin abrir todavía ningún dispositivo.</summary>
    /// <returns>Servicio nuevo que el visor debe detener y liberar al terminar.</returns>
    public ICameraService Create()
    {
        // Cada llamada crea su propio VideoCapture y su propio bloqueo de lectura.
        return new OpenCvCameraService();
    }
}
