using OpenCvSharp;
using Parking.Application.EntityService;
using Parking.Infrastructure.ExternalServices;
using Parking.UI.Windows.ViewModels.Base;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;

namespace Parking.UI.Windows.ViewModels
{
    public class PlateReaderViewModel : BaseViewModel
    {
        private static readonly TimeSpan AutoDetectionInterval = TimeSpan.FromMilliseconds(900);
        private static readonly TimeSpan DetectionDisplayTime = TimeSpan.FromSeconds(3);

        private readonly ICameraService _cameraService;
        private readonly IPlateService _plateService;
        private readonly IEntryService _entryService;
        private readonly object _frameSyncRoot = new();

        private CancellationTokenSource? _previewCancellation;
        private byte[] _latestFrame = Array.Empty<byte>();

        private string _plateNumber = string.Empty;
        private string _statusMessage = "Listo para iniciar.";
        private decimal _amountToCharge = 0;
        private BitmapSource? _cameraPreview;
        private bool _isCameraRunning;

        private int _autoDetectionInProgress;
        private DateTime _lastAutoDetectionUtc = DateTime.MinValue;
        private PlateDetectionResult? _currentDetection;
        private DateTime _lastDetectionTime = DateTime.MinValue;

        private readonly IQrService _qrService; // Faltaba asignar tu servicio
        private int _qrDetectionInProgress;
        private DateTime _lastQrDetectionUtc = DateTime.MinValue;
        private string _lastScannedQr = string.Empty;
        private DateTime _lastQrScanTime = DateTime.MinValue;
        private static readonly TimeSpan QrDetectionInterval = TimeSpan.FromMilliseconds(1000); // 1 escaneo por segundo

        // Propiedades para la Vista
        public string PlateNumber { get => _plateNumber; set => SetProperty(ref _plateNumber, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public decimal AmountToCharge { get => _amountToCharge; set => SetProperty(ref _amountToCharge, value); }
        public BitmapSource? CameraPreview { get => _cameraPreview; set => SetProperty(ref _cameraPreview, value); }
        public bool IsCameraRunning { get => _isCameraRunning; set => SetProperty(ref _isCameraRunning, value); }

        // Comandos
        public ICommand StartCameraCommand { get; }
        public ICommand StopCameraCommand { get; }
        public ICommand SavePlateCommand { get; }
        public ICommand RegisterExitCommand { get; }

        public PlateReaderViewModel(
            ICameraService cameraService,
            IPlateService plateService,
            IQrService qrService,
            IEntryService entryService)
        {
            _cameraService = cameraService;
            _plateService = plateService;
            _entryService = entryService;
            _qrService = qrService;

            StartCameraCommand = new AsyncRelayCommand(_ => StartCameraAsync());
            StopCameraCommand = new AsyncRelayCommand(_ => StopCameraAsync());
            SavePlateCommand = new AsyncRelayCommand(_ => SaveCorrectedPlateAsync());
            RegisterExitCommand = new AsyncRelayCommand(_ => RegisterExitAsync());

            _ = StartCameraAsync();
        }

        private async Task SaveCorrectedPlateAsync()
        {
            if (string.IsNullOrWhiteSpace(PlateNumber)) return;
            try
            {
                AmountToCharge = 0; // Resetear visor de cobro al entrar
                bool success = await _entryService.RegisterEntryAsync(PlateNumber.Trim().ToUpper());
                StatusMessage = success ? $"✅ ENTRADA: {PlateNumber}" : "❌ Error al registrar entrada.";
            }
            catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        }

        private async Task RegisterExitAsync()
        {
            if (string.IsNullOrWhiteSpace(PlateNumber)) return;
            string plate = PlateNumber.Trim().ToUpper();

            try
            {
                // 1. Obtener la sesión antes de cerrarla para calcular el monto en el ViewModel
                var session = await _entryService.GetActiveSessionByPlateAsync(plate);
                if (session == null)
                {
                    StatusMessage = $"❌ No hay sesión activa para {plate}.";
                    AmountToCharge = 0;
                    return;
                }

                // 2. Registrar salida (Cálculo de hora o fracción hecho en el Service)
                bool success = await _entryService.RegisterExitByPlateAsync(plate);

                if (success)
                {
                    // 3. Mostrar el cobro (Lógica de hora o fracción: $1 x cada hora iniciada)
                    TimeSpan duration = DateTime.UtcNow - session.entry_time;
                    decimal hoursToCharge = (decimal)Math.Ceiling(duration.TotalHours);
                    if (hoursToCharge < 1) hoursToCharge = 1;

                    AmountToCharge = hoursToCharge * 1.00m;
                    StatusMessage = $"✅ SALIDA: {plate} | Total: {AmountToCharge:C2}";
                }
            }
            catch (Exception ex) { StatusMessage = $"Error en salida: {ex.Message}"; }
        }

        // --- MÉTODOS DE CÁMARA (Recuperados para corregir tus errores) ---

        private async Task StartCameraAsync()
        {
            if (IsCameraRunning) return;
            bool started = await _cameraService.StartCameraAsync();
            if (!started) { StatusMessage = "No se pudo abrir la cámara."; return; }
            IsCameraRunning = true;
            _previewCancellation?.Cancel();
            _previewCancellation = new CancellationTokenSource();
            _ = RunPreviewLoopAsync(_previewCancellation.Token);
        }

        private async Task StopCameraAsync()
        {
            if (!IsCameraRunning) return;
            _previewCancellation?.Cancel();
            await _cameraService.StopCameraAsync();
            CameraPreview = null;
            IsCameraRunning = false;
        }

        private async Task RunPreviewLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    byte[] currentFrame = await _cameraService.CaptureFrameAsync();
                    if (currentFrame.Length > 0)
                    {
                        SaveLatestFrame(currentFrame);
                        byte[] frameToShow = currentFrame;

                        if (_currentDetection != null && (DateTime.UtcNow - _lastDetectionTime) < DetectionDisplayTime)
                        {
                            frameToShow = DrawDetectionOnFrame(currentFrame, _currentDetection);
                        }

                        var preview = BuildBitmapSource(frameToShow);
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => CameraPreview = preview);
                        TryQueueAutomaticEntry(currentFrame);
                        TryQueueQrDetection(currentFrame);
                    }
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch { }
        }

        private void TryQueueQrDetection(byte[] currentFrame)
        {
            // Escanea 1 vez por segundo para no saturar el CPU junto con la detección de placas
            if (currentFrame.Length == 0 || DateTime.UtcNow - _lastQrDetectionUtc < QrDetectionInterval) return;

            // Previene múltiples escaneos paralelos
            if (Interlocked.CompareExchange(ref _qrDetectionInProgress, 1, 0) != 0) return;

            _lastQrDetectionUtc = DateTime.UtcNow;
            byte[] frameCopy = (byte[])currentFrame.Clone();
            _ = ProcessQrDetectionAsync(frameCopy);
        }

        private async Task ProcessQrDetectionAsync(byte[] frame)
        {
            try
            {
                string qrText = await _qrService.ReadQrAsync(frame);

                // Verifica si leyó algo y si tiene el formato correcto de tu base de datos
                if (!string.IsNullOrWhiteSpace(qrText) && qrText.StartsWith("SESSION-"))
                {
                    // Evita cobrar múltiples veces si el cliente deja el papel frente a la cámara
                    if (_lastScannedQr == qrText && (DateTime.UtcNow - _lastQrScanTime) < TimeSpan.FromSeconds(5))
                        return;

                    _lastScannedQr = qrText;
                    _lastQrScanTime = DateTime.UtcNow;

                    await RegisterExitFromQrAsync(qrText);
                }
            }
            finally { Interlocked.Exchange(ref _qrDetectionInProgress, 0); }
        }

        private async Task RegisterExitFromQrAsync(string qrCode)
        {
            try
            {
                // 1. Obtener los datos de la sesión usando el QR
                var session = await _entryService.GetActiveSessionByQrAsync(qrCode);
                if (session == null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        StatusMessage = $"❌ QR Inválido o sesión ya cerrada.");
                    return;
                }

                // 2. Registrar la salida usando tu método actual
                bool success = await _entryService.RegisterExitByQrAsync(qrCode);

                if (success)
                {
                    // 3. Calcular el cobro para mostrarlo al operador (Misma lógica de hora o fracción)
                    TimeSpan duration = DateTime.UtcNow - session.entry_time;
                    decimal hoursToCharge = (decimal)Math.Ceiling(duration.TotalHours);
                    if (hoursToCharge < 1) hoursToCharge = 1;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        AmountToCharge = hoursToCharge * 1.00m;
                        PlateNumber = session.plate; // Autocompleta la placa en el textbox
                        StatusMessage = $"✅ SALIDA POR QR: {session.plate} | Total: {AmountToCharge:C2}";
                    });
                }
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    StatusMessage = $"Error en salida QR: {ex.Message}");
            }
        }

        private void TryQueueAutomaticEntry(byte[] currentFrame)
        {
            if (currentFrame.Length == 0 || DateTime.UtcNow - _lastAutoDetectionUtc < AutoDetectionInterval) return;
            if (Interlocked.CompareExchange(ref _autoDetectionInProgress, 1, 0) != 0) return;

            _lastAutoDetectionUtc = DateTime.UtcNow;
            byte[] frameCopy = (byte[])currentFrame.Clone();
            _ = ProcessAutomaticDetectionAsync(frameCopy);
        }

        private async Task ProcessAutomaticDetectionAsync(byte[] frame)
        {
            try
            {
                var detection = await ((PlateReaderService)_plateService).DetectPlateWithRegionsAsync(frame);
                if (!string.IsNullOrWhiteSpace(detection.PlateNumber))
                {
                    _currentDetection = detection;
                    _lastDetectionTime = DateTime.UtcNow;
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => PlateNumber = detection.PlateNumber);
                }
            }
            finally { Interlocked.Exchange(ref _autoDetectionInProgress, 0); }
        }

        private byte[] DrawDetectionOnFrame(byte[] frameBytes, PlateDetectionResult detection)
        {
            using var mat = Cv2.ImDecode(frameBytes, ImreadModes.Color);
            if (detection.HasDetection)
            {
                foreach (var rect in detection.DetectedRegions)
                {
                    Cv2.Rectangle(mat, rect, Scalar.LimeGreen, 4, LineTypes.AntiAlias);
                    if (!string.IsNullOrWhiteSpace(detection.PlateNumber))
                        Cv2.PutText(mat, detection.PlateNumber, new OpenCvSharp.Point(rect.X, rect.Y - 15), HersheyFonts.HersheySimplex, 1.2, Scalar.LimeGreen, 3);
                }
            }
            return mat.ToBytes(".png");
        }

        private void SaveLatestFrame(byte[] frame) { lock (_frameSyncRoot) _latestFrame = (byte[])frame.Clone(); }

        private static BitmapSource BuildBitmapSource(byte[] frameBytes)
        {
            using var ms = new MemoryStream(frameBytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}