namespace Parking.Application.EntityService;

/// <summary>Determina cómo se interpreta la dirección de vídeo de una fuente de red.</summary>
public enum CameraAddressProfile
{
    /// <summary>Exige una URL completa para cualquier cámara IP compatible.</summary>
    Generic,
    /// <summary>Permite usar la IP abreviada y los valores predeterminados de DroidCam.</summary>
    DroidCam
}
