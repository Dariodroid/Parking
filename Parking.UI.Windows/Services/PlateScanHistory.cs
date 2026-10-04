namespace Parking.UI.Windows.Services;

/// <summary>Recuerda lecturas recientes para impedir entradas y salidas repetidas ante la cámara.</summary>
public sealed class PlateScanHistory
{
    private static readonly TimeSpan PlateReentryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ExitEntryCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan QrRepeatInterval = TimeSpan.FromSeconds(5);
    private readonly object _sync = new();
    private string _handledPlate = string.Empty;
    private DateTime _handledPlateLastSeenUtc = DateTime.MinValue;
    private string _lastExitedPlate = string.Empty;
    private DateTime _lastExitUtc = DateTime.MinValue;
    private string _lastScannedQr = string.Empty;
    private DateTime _lastQrScanUtc = DateTime.MinValue;

    /// <summary>Indica si una salida reciente impide registrar de nuevo la misma entrada.</summary>
    public bool WasRecentlyExited(string plate, DateTime nowUtc)
    {
        lock (_sync)
            return string.Equals(plate, _lastExitedPlate, StringComparison.OrdinalIgnoreCase)
                && nowUtc - _lastExitUtc < ExitEntryCooldown;
    }

    /// <summary>Marca una placa que ya se mostró o registró mientras continúa ante la cámara.</summary>
    public void MarkPlateHandled(string plate, DateTime nowUtc)
    {
        lock (_sync)
        {
            _handledPlate = plate;
            _handledPlateLastSeenUtc = nowUtc;
        }
    }

    /// <summary>Conserva la presencia de una placa conocida cuando el OCR falla en algunos fotogramas.</summary>
    public void KeepHandledPlateVisible(DateTime nowUtc)
    {
        lock (_sync)
        {
            if (!string.IsNullOrEmpty(_handledPlate))
                _handledPlateLastSeenUtc = nowUtc;
        }
    }

    /// <summary>Evita interpretar la misma placa visible como otro vehículo.</summary>
    public bool ShouldIgnoreRepeatedPlate(string plate, DateTime nowUtc)
    {
        lock (_sync)
        {
            if (!string.Equals(_handledPlate, plate, StringComparison.OrdinalIgnoreCase)
                || nowUtc - _handledPlateLastSeenUtc >= PlateReentryInterval)
                return false;

            _handledPlateLastSeenUtc = nowUtc;
            return true;
        }
    }

    /// <summary>Evita cerrar dos veces una sesión mientras el ticket QR siga visible.</summary>
    public bool ShouldIgnoreRepeatedQr(string qr, DateTime nowUtc)
    {
        lock (_sync)
        {
            bool repeated = string.Equals(_lastScannedQr, qr, StringComparison.OrdinalIgnoreCase)
                && nowUtc - _lastQrScanUtc < QrRepeatInterval;
            _lastScannedQr = qr;
            _lastQrScanUtc = nowUtc;
            return repeated;
        }
    }

    /// <summary>Recuerda la salida confirmada para impedir lecturas inmediatas de la misma sesión.</summary>
    public void MarkExited(string plate, string? qr, DateTime nowUtc)
    {
        lock (_sync)
        {
            _lastExitedPlate = plate;
            _lastExitUtc = nowUtc;
            _lastScannedQr = qr ?? string.Empty;
            _lastQrScanUtc = nowUtc;
        }
    }
}
