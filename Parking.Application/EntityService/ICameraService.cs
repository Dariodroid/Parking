namespace Parking.Application.EntityService
{
    public interface ICameraService
    {
        // Indica si el dispositivo de cámara ya quedó abierto y listo para leer frames.
        bool IsCameraRunning { get; }

        // Abre la cámara usando el índice configurado.
        Task<bool> StartCameraAsync(int cameraIndex = 0);

        // Captura el frame actual y lo devuelve como bytes JPEG.
        Task<byte[]> CaptureFrameAsync();

        // Libera el dispositivo para que Windows u otra app lo pueda reutilizar.
        Task StopCameraAsync();
    }
}
