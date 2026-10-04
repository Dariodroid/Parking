using Parking.UI.Windows.Interfaces;
using Parking.UI.Windows.ViewModels;
using System.IO;
using System.Windows.Media.Imaging;

namespace Parking.UI.Windows.Services;

/// <summary>Mantiene la vista en directo y detecta pérdidas de imagen de cada cámara.</summary>
public sealed class CameraPreviewService
{
    private static readonly TimeSpan DetectionDisplayTime = TimeSpan.FromSeconds(3);
    private readonly IFrameOverlayRenderer _overlay;
    private readonly CameraHealthMonitor _cameraHealth;

    /// <summary>Recibe el dibujante del visor y el registro de fallos de cámara.</summary>
    public CameraPreviewService(IFrameOverlayRenderer overlay, CameraHealthMonitor cameraHealth)
    {
        _overlay = overlay;
        _cameraHealth = cameraHealth;
    }

    /// <summary>Lee fotogramas hasta la cancelación y entrega cada imagen a los detectores.</summary>
    public async Task RunAsync(CameraFeedViewModel feed, Action<CameraFeedViewModel, byte[]> queueDetection,
        Action notifyRunningChanged, CancellationToken cancellationToken)
    {
        DateTime lastFrameUtc = DateTime.UtcNow;
        DateTime lastPreviewUtc = DateTime.MinValue;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] currentFrame = await feed.Capture.CaptureFrameAsync().ConfigureAwait(false);
                if (currentFrame.Length > 0)
                {
                    DateTime now = DateTime.UtcNow;
                    lastFrameUtc = now;
                    _cameraHealth.Clear(feed.Title);
                    lastPreviewUtc = await UpdatePreviewIfDueAsync(feed, currentFrame, now, lastPreviewUtc);
                    queueDetection(feed, currentFrame);
                }
                if (currentFrame.Length == 0 &&
                    await HandleMissingFrameAsync(feed, lastFrameUtc, cancellationToken)) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                feed.Status = "Se interrumpió el vídeo de esta cámara.");
            _cameraHealth.ReportFailure(feed.Title, feed.Status);
        }
        finally
        {
            // El cierre solicitado por el operador espera al bucle antes de liberar el driver.
            if (!cancellationToken.IsCancellationRequested)
            {
                try { await feed.Capture.StopCameraAsync(); }
                catch (Exception) { }
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    feed.Preview = null;
                    feed.ActiveSource = null;
                    feed.IsRunning = false;
                    notifyRunningChanged();
                });
            }
        }
    }

    /// <summary>Actualiza el visor a un máximo aproximado de quince imágenes por segundo.</summary>
    private async Task<DateTime> UpdatePreviewIfDueAsync(CameraFeedViewModel feed, byte[] frame,
        DateTime now, DateTime lastPreviewUtc)
    {
        if (now - lastPreviewUtc < TimeSpan.FromMilliseconds(66)) return lastPreviewUtc;
        var visibleDetection = now - feed.LastDetectionUtc < DetectionDisplayTime
            ? feed.CurrentDetection : null;
        byte[] image = _overlay.Render(frame, visibleDetection);
        var preview = BuildBitmapSource(image);
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => feed.Preview = preview);
        return now;
    }

    /// <summary>Avisa cuando una fuente abierta deja de entregar imágenes durante cinco segundos.</summary>
    private async Task<bool> HandleMissingFrameAsync(CameraFeedViewModel feed,
        DateTime lastFrameUtc, CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - lastFrameUtc > TimeSpan.FromSeconds(5))
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                feed.Status = "Esta cámara dejó de entregar imagen. Pulse Buscar o Iniciar.");
            _cameraHealth.ReportFailure(feed.Title, feed.Status);
            return true;
        }
        await Task.Delay(50, cancellationToken);
        return false;
    }

    /// <summary>Convierte los bytes en una imagen WPF que puede mostrarse desde otro hilo.</summary>
    private static BitmapSource BuildBitmapSource(byte[] frameBytes)
    {
        using var stream = new MemoryStream(frameBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
