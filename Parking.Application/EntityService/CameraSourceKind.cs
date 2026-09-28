namespace Parking.Application.EntityService;

/// <summary>Indica cómo OpenCV obtiene los fotogramas de una cámara.</summary>
public enum CameraSourceKind
{
    /// <summary>Dispositivo de vídeo reconocido por Windows, incluido USB o DroidCam virtual.</summary>
    WindowsDevice,
    /// <summary>Flujo de vídeo recibido desde una URL de red.</summary>
    NetworkStream
}
