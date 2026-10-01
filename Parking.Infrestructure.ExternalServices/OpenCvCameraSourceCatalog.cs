using OpenCvSharp;
using Parking.Application.Ports;

namespace Parking.Infrastructure.ExternalServices;

/// <summary>Detecta cámaras locales por los índices que OpenCV puede abrir.</summary>
public sealed class OpenCvCameraSourceCatalog : ICameraSourceCatalog
{
    // OpenCV no expone una enumeración de dispositivos; se prueban índices consecutivos.
    private const int MaximumDeviceIndex = 9;

    /// <summary>Prueba dispositivos de Windows y devuelve solo los índices utilizables.</summary>
    /// <param name="cancellationToken">Interrumpe la búsqueda entre dispositivos.</param>
    /// <returns>Fuentes locales; un teléfono con DroidCam aparece si instala cámara virtual.</returns>
    public Task<IReadOnlyList<CameraSource>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        // La apertura del driver no debe bloquear el hilo de WPF.
        return Task.Run<IReadOnlyList<CameraSource>>(() =>
        {
            // La lista se construye de cero para reflejar conexiones USB recientes.
            var sources = new List<CameraSource>();
            for (var index = 0; index <= MaximumDeviceIndex; index++)
            {
                // El usuario puede salir de la vista mientras se prueban los índices.
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // Se prueban los mismos dos backends y en el mismo orden que al abrir.
                    if (!CanOpen(index, VideoCaptureAPIs.DSHOW)
                        && !CanOpen(index, VideoCaptureAPIs.ANY)) continue;

                    // Se suelta inmediatamente para que un visor pueda abrirla después.
                    sources.Add(new CameraSource($"device:{index}", $"Cámara Windows {index}",
                        CameraSourceKind.WindowsDevice, DeviceIndex: index));
                }
                catch (Exception)
                {
                    // Un índice inexistente o un driver defectuoso no impide ver los demás.
                }
            }

            // La interfaz mantiene una opción separada para URL RTSP/HTTP.
            return sources;
        }, cancellationToken);
    }

    /// <summary>Comprueba un índice con un backend y libera la captura de inmediato.</summary>
    /// <param name="index">Posición de cámara que se va a probar.</param>
    /// <param name="backend">API de vídeo usada por OpenCV.</param>
    /// <returns>Verdadero cuando el driver confirma que la cámara se abrió.</returns>
    private static bool CanOpen(int index, VideoCaptureAPIs backend)
    {
        // using libera el dispositivo incluso cuando la comprobación resulta falsa.
        using var capture = new VideoCapture(index, backend);
        return capture.IsOpened();
    }
}
