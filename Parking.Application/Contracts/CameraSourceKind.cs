namespace Parking.Application.Contracts;

/// <summary>Indica cómo OpenCV obtiene los fotogramas de una cámara.</summary>
public enum CameraSourceKind
{
    /// <summary>Dispositivo de vídeo reconocido por Windows, incluidas webcams USB o virtuales.</summary>
    WindowsDevice,
    /// <summary>Flujo de vídeo recibido desde una URL de red.</summary>
    NetworkStream
}
