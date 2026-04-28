using OpenCvSharp;
using Parking.Application.EntityService;

namespace Parking.Infrastructure.ExternalServices
{
    public sealed class OpenCvCameraService : ICameraService, IDisposable
    {
        private readonly object _syncRoot = new();
        private VideoCapture? _capture;
        private bool _disposed;

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

        public Task<bool> StartCameraAsync(int cameraIndex = 0)
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

                    // DSHOW suele ser la opción más estable para webcams en Windows.
                    _capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);

                    // Si DSHOW falla, hacemos un segundo intento con el backend por defecto.
                    if (!_capture.IsOpened())
                    {
                        _capture.Dispose();
                        _capture = new VideoCapture(cameraIndex);
                    }

                    if (!_capture.IsOpened())
                    {
                        ReleaseCapture();
                        return false;
                    }

                    // Estas propiedades son sugerencias al driver; algunos dispositivos las respetan y otros no.
                    _capture.FrameWidth = 1280;
                    _capture.FrameHeight = 720;
                    _capture.Fps = 30;

                    return true;
                }
            });
        }

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

        private void ReleaseCapture()
        {
            if (_capture is null)
                return;

            if (_capture.IsOpened())
                _capture.Release();

            _capture.Dispose();
            _capture = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpenCvCameraService));
        }
    }
}
