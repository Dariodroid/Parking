using OpenCvSharp;
using Parking.Application.Ports;

namespace Parking.Infrastructure.ExternalServices
{
    /// <summary>Posee una captura OpenCV y entrega fotogramas de una sola fuente seleccionada.</summary>
    public sealed class OpenCvCameraService : ICameraService, IDisposable
    {
        private readonly object _syncRoot = new();
        private VideoCapture? _capture;
        private bool _disposed;

        /// <summary>Verdadero cuando esta instancia mantiene una fuente abierta.</summary>
        public bool IsCameraRunning
        {
            get
            {
                lock (_syncRoot)
                {
                    return _capture?.IsOpened() == true;
                }
            }
        }

        /// <summary>Abre una cámara local usando la firma anterior del servicio.</summary>
        /// <param name="cameraIndex">Índice de la cámara en Windows.</param>
        /// <returns>Verdadero si OpenCV pudo abrirla.</returns>
        public Task<bool> StartCameraAsync(int cameraIndex = 0)
        {
            // La sobrecarga antigua usa la misma ruta de apertura que los visores nuevos.
            var source = new CameraSource($"device:{cameraIndex}", $"Cámara Windows {cameraIndex}",
                CameraSourceKind.WindowsDevice, DeviceIndex: cameraIndex);
            return StartCameraAsync(source);
        }

        /// <summary>Abre una fuente local o de red en esta instancia independiente.</summary>
        /// <param name="source">Cámara elegida para el visor.</param>
        /// <returns>Verdadero cuando el driver o el flujo de red queda abierto.</returns>
        public Task<bool> StartCameraAsync(CameraSource source)
        {
            // Abrir la cámara puede tardar unas décimas; lo sacamos del hilo UI.
            return Task.Run(() =>
            {
                lock (_syncRoot)
                {
                    ThrowIfDisposed();

                    if (_capture?.IsOpened() == true)
                        return true;

                    ReleaseCapture();

                    if (source.Kind == CameraSourceKind.NetworkStream)
                    {
                        // FFmpeg lee RTSP y flujos HTTP/MJPEG; sus tiempos limitan
                        // bloqueos al abrir o leer una cámara de red desconectada.
                        if (string.IsNullOrWhiteSpace(source.StreamUrl)) return false;
                        // La versión local de OpenCvSharp no expone los nombres de
                        // CAP_PROP_OPEN_TIMEOUT_MSEC (53) y READ_TIMEOUT_MSEC (54).
                        _capture = new VideoCapture(source.StreamUrl, VideoCaptureAPIs.FFMPEG,
                            [53, 5000, 54, 3000]);
                        if (!_capture.IsOpened())
                        {
                            // El backend predeterminado puede aceptar ciertos MJPEG HTTP.
                            _capture.Dispose();
                            _capture = new VideoCapture(source.StreamUrl);
                        }
                    }
                    else
                    {
                        // DirectShow suele ser estable para webcam USB y cámaras virtuales.
                        if (source.DeviceIndex is not int index) return false;
                        _capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);

                        // Algunos drivers solo aceptan el backend predeterminado.
                        if (!_capture.IsOpened())
                        {
                            _capture.Dispose();
                            _capture = new VideoCapture(index);
                        }
                    }

                    if (!_capture.IsOpened())
                    {
                        ReleaseCapture();
                        return false;
                    }

                    if (source.Kind == CameraSourceKind.WindowsDevice)
                    {
                        // Las resoluciones son sugerencias al driver local, no a un flujo RTSP.
                        _capture.FrameWidth = 1280;
                        _capture.FrameHeight = 720;
                        _capture.Fps = 30;
                    }

                    return true;
                }
            });
        }

        /// <summary>Lee un fotograma de la fuente actual y lo codifica como JPEG.</summary>
        /// <returns>Imagen JPEG, o arreglo vacío si la captura no entrega imagen.</returns>
        public Task<byte[]> CaptureFrameAsync()
        {
            return Task.Run(() =>
            {
                lock (_syncRoot)
                {
                    ThrowIfDisposed();

                    if (_capture?.IsOpened() != true)
                        return Array.Empty<byte>();

                    using var frame = new Mat();

                    // Read intenta capturar y decodificar el frame en una sola llamada.
                    if (!_capture.Read(frame) || frame.Empty())
                        return Array.Empty<byte>();

                    // Codificamos a JPEG para mover un bloque pequeño de datos hacia la UI/OCR.
                    return frame.ToBytes(".jpg");
                }
            });
        }

        /// <summary>Libera la captura en un hilo de trabajo para no bloquear la interfaz.</summary>
        /// <returns>Tarea completada cuando OpenCV suelta la fuente.</returns>
        public Task StopCameraAsync()
        {
            return Task.Run(() =>
            {
                lock (_syncRoot)
                {
                    ReleaseCapture();
                }
            });
        }

        /// <summary>Libera definitivamente los recursos que posee esta instancia.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_syncRoot)
            {
                if (_disposed)
                    return;

                ReleaseCapture();
                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>Suprime la fuente abierta y descarta su VideoCapture.</summary>
        private void ReleaseCapture()
        {
            // Nada que liberar cuando esta instancia aún no ha abierto una fuente.
            if (_capture is null)
                return;

            // Desvinculamos primero la captura para que un fallo de Release no la deje reutilizable.
            var capture = _capture;
            _capture = null;
            try
            {
                // Cerramos explícitamente el flujo antes de destruir el objeto nativo.
                if (capture.IsOpened())
                    capture.Release();
            }
            finally
            {
                // Dispose se ejecuta incluso si el driver falla al consultar o cerrar la cámara.
                capture.Dispose();
            }
        }

        /// <summary>Impide usar una captura que ya fue descartada.</summary>
        /// <exception cref="ObjectDisposedException">La instancia ya fue liberada.</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpenCvCameraService));
        }
    }
}
