namespace Parking.Application.EntityService
{
    /// <summary>Captura fotogramas de una sola fuente; cada visor recibe una instancia propia.</summary>
    public interface ICameraService
    {
        /// <summary>Indica si la fuente quedó abierta y lista para leer fotogramas.</summary>
        bool IsCameraRunning { get; }

        /// <summary>Abre un dispositivo Windows por su índice; conserva la firma anterior.</summary>
        /// <param name="cameraIndex">Índice que OpenCV asigna a la cámara.</param>
        /// <returns>Verdadero cuando el dispositivo queda abierto.</returns>
        Task<bool> StartCameraAsync(int cameraIndex = 0);

        /// <summary>Abre el dispositivo o flujo seleccionado para este visor.</summary>
        /// <param name="source">Origen local o URL de red con su configuración.</param>
        /// <returns>Verdadero cuando la fuente empieza a entregar vídeo.</returns>
        Task<bool> StartCameraAsync(CameraSource source);

        /// <summary>Captura el fotograma actual como bytes JPEG.</summary>
        /// <returns>Imagen JPEG o arreglo vacío cuando aún no hay fotograma.</returns>
        Task<byte[]> CaptureFrameAsync();

        /// <summary>Libera el dispositivo para que el sistema pueda reutilizarlo.</summary>
        /// <returns>Tarea que termina después de soltar la fuente.</returns>
        Task StopCameraAsync();
    }
}
