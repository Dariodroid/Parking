using OpenCvSharp;
using Parking.Application.EntityService;
using Parking.Application.Services;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.Infrastructure.DataAccess.Repository;
using Parking.Infrastructure.ExternalServices;
using Parking.UI.Windows.Helpers;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Parking.UI.Windows.ViewModels
{
    public class PlateReaderViewModel : BaseViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly Ivehicle_typeRepository _vehicleTypeRepository;
        private readonly IParkingSlotRepository _slotRepo; 
        private static readonly TimeSpan AutoDetectionInterval = TimeSpan.FromMilliseconds(900);
        private static readonly TimeSpan DetectionDisplayTime = TimeSpan.FromSeconds(3);

        private readonly ICameraService _cameraService;
        private readonly IPlateService _plateService;
        private readonly IEntryService _entryService;
        private readonly IParkingStatusNotifier _parkingStatusNotifier;
        private readonly object _frameSyncRoot = new();

        private CancellationTokenSource? _previewCancellation;
        private byte[] _latestFrame = Array.Empty<byte>();

        private string _plateNumber = string.Empty;
        private string _statusMessage = "Listo para iniciar.";
        private decimal _amountToCharge = 0;
        private BitmapSource? _cameraPreview;
        private bool _isCameraRunning;

        // Propiedades para las tarjetas superiores
        private int _totalSlots;
        private int _occupiedSlots;
        private int _freeSlots;

        public int TotalSlots { get => _totalSlots; set => SetProperty(ref _totalSlots, value); }
        public int OccupiedSlots { get => _occupiedSlots; set => SetProperty(ref _occupiedSlots, value); }
        public int FreeSlots { get => _freeSlots; set => SetProperty(ref _freeSlots, value); }

        private int _autoDetectionInProgress;
        private DateTime _lastAutoDetectionUtc = DateTime.MinValue;
        private PlateDetectionResult? _currentDetection;
        private DateTime _lastDetectionTime = DateTime.MinValue;

        private readonly IQrService _qrService;
        private int _qrDetectionInProgress;
        private DateTime _lastQrDetectionUtc = DateTime.MinValue;
        private string _lastScannedQr = string.Empty;
        private DateTime _lastQrScanTime = DateTime.MinValue;
        private static readonly TimeSpan QrDetectionInterval = TimeSpan.FromMilliseconds(1000);

        public string PlateNumber
        {
            get => _plateNumber;
            set
            {
                var formattedPlate = PlateFormatter.Format(value);

                if (!SetProperty(ref _plateNumber, formattedPlate)
                    && !string.Equals(value, formattedPlate, StringComparison.Ordinal))
                {
                    // Restores the displayed value when an invalid or excess character is typed.
                    OnPropertyChanged();
                }
            }
        }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public decimal AmountToCharge { get => _amountToCharge; set => SetProperty(ref _amountToCharge, value); }
        public BitmapSource? CameraPreview { get => _cameraPreview; set => SetProperty(ref _cameraPreview, value); }
        public bool IsCameraRunning { get => _isCameraRunning; set => SetProperty(ref _isCameraRunning, value); }

        public ObservableCollection<vehicle_type> VehicleTypes { get; } = new ObservableCollection<vehicle_type>();

        private vehicle_type? _selectedVehicleType;

        public vehicle_type? SelectedVehicleType
        {
            get => _selectedVehicleType;
            set => SetProperty(ref _selectedVehicleType, value);
        }

        public ICommand StartCameraCommand { get; }
        public ICommand StopCameraCommand { get; }
        public ICommand SavePlateCommand { get; }
        public ICommand RegisterExitCommand { get; }
        public ICommand SelectVehicleTypeCommand { get; }

        public PlateReaderViewModel(
            ICameraService cameraService,
            IPlateService plateService,
            IQrService qrService,
            IEntryService entryService,
            Ivehicle_typeRepository vehicleTypeRepository,
            IParkingSlotRepository slotRepo,
            IParkingStatusNotifier parkingStatusNotifier,
            IDialogService dialogService) 
        {
            _dialogService = dialogService;
            _cameraService = cameraService;
            _plateService = plateService;
            _entryService = entryService;
            _qrService = qrService;
            _vehicleTypeRepository = vehicleTypeRepository;
            _slotRepo = slotRepo;
            _parkingStatusNotifier = parkingStatusNotifier;

            StartCameraCommand = new AsyncRelayCommand(_ => StartCameraAsync());
            StopCameraCommand = new AsyncRelayCommand(_ => StopCameraAsync());
            SavePlateCommand = new AsyncRelayCommand(_ => SaveCorrectedPlateAsync());
            RegisterExitCommand = new AsyncRelayCommand(_ => RegisterExitAsync());
            SelectVehicleTypeCommand = new RelayCommand(param =>
            {
                if (param is vehicle_type selectedType) SelectedVehicleType = selectedType;
            });

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Las consultas comparten el mismo DbContext; deben ejecutarse en secuencia.
                await LoadVehicleTypesAsync();
                await LoadSlotStatsAsync();
                await StartCameraAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error", $"Error cargando la pantalla: {ex.Message}");
            }
        }

        private async Task LoadSlotStatsAsync()
        {
            var slots = (await _slotRepo.GetAllAsync()).ToList();
            int totalSlots = slots.Count;
            int occupiedSlots = slots.Count(s => s.is_occupied);

            void UpdateStats()
            {
                TotalSlots = totalSlots;
                OccupiedSlots = occupiedSlots;
                FreeSlots = totalSlots - occupiedSlots;
            }

            // 🟢 CORREGIDO: Nombre completo para evitar colisión con Parking.Application
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                UpdateStats();
            }
            else
            {
                // 🟢 CORREGIDO: Nombre completo para evitar colisión con Parking.Application
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(UpdateStats);
            }
        }

        private async Task LoadVehicleTypesAsync()
        {
            VehicleTypes.Clear();
            var items = await _vehicleTypeRepository.GetAllAsync();
            foreach (var item in items.Where(x => x.is_active && !x.is_deleted))
            {
                VehicleTypes.Add(item);
            }
            SelectedVehicleType = VehicleTypes.FirstOrDefault();
        }

        private async Task SaveCorrectedPlateAsync()
        {
            if (string.IsNullOrWhiteSpace(PlateNumber)) return;
            if (SelectedVehicleType == null)
            {
                _dialogService.ShowWarning("Atención", "Seleccione un tipo de vehículo.");
                return;
            }

            try
            {
                AmountToCharge = 0;
                string? assignedSlot = await _entryService.RegisterEntryAsync(PlateNumber.Trim().ToUpper(), SelectedVehicleType.id);

                if (assignedSlot == "EXISTENTE")
                {
                    _dialogService.ShowInfo("Información", $"El vehículo {PlateNumber} ya tiene una sesión activa.");
                }
                else if (!string.IsNullOrEmpty(assignedSlot))
                {
                    _dialogService.ShowSuccess("¡Éxito!", $"ENTRADA: {PlateNumber} asignado al puesto {assignedSlot}.");
                    await LoadSlotStatsAsync(); // Actualizar tarjetas
                }
                else
                {
                    _dialogService.ShowError("Error", "Parqueadero lleno o error al registrar entrada.");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error", ex.Message);
            }
        }

        private async Task RegisterExitAsync()
        {
            if (string.IsNullOrWhiteSpace(PlateNumber)) return;
            string plate = PlateNumber.Trim().ToUpper();

            try
            {
                var session = await _entryService.GetActiveSessionByPlateAsync(plate);
                if (session == null)
                {
                    _dialogService.ShowWarning("Atención", $"No hay sesión activa para {plate}.");
                    AmountToCharge = 0;
                    return;
                }

                bool success = await _entryService.RegisterExitByPlateAsync(plate);

                if (success)
                {
                    TimeSpan duration = DateTime.UtcNow - session.entry_time;
                    decimal hoursToCharge = (decimal)Math.Ceiling(duration.TotalHours);
                    if (hoursToCharge < 1) hoursToCharge = 1;

                    AmountToCharge = hoursToCharge * 1.00m;
                    _dialogService.ShowSuccess("¡Éxito!", $"SALIDA: {plate} | Total: {AmountToCharge:C2}");
                    await LoadSlotStatsAsync();
                    _parkingStatusNotifier.NotifyParkingStatusChanged();
                }
            }
            catch (Exception ex) { _dialogService.ShowError("Error", $"Error en salida: {ex.Message}"); }
        }

        private async Task StartCameraAsync()
        {
            if (IsCameraRunning) return;
            bool started = await _cameraService.StartCameraAsync();
            if (!started) { _dialogService.ShowError("Error", "No se pudo abrir la cámara."); return; }
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
            if (currentFrame.Length == 0 || DateTime.UtcNow - _lastQrDetectionUtc < QrDetectionInterval) return;
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
                if (!string.IsNullOrWhiteSpace(qrText) && qrText.StartsWith("SESSION-"))
                {
                    if (_lastScannedQr == qrText && (DateTime.UtcNow - _lastQrScanTime) < TimeSpan.FromSeconds(5)) return;

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
                var session = await _entryService.GetActiveSessionByQrAsync(qrCode);
                if (session == null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = $"❌ QR Inválido o sesión ya cerrada.");
                    return;
                }

                bool success = await _entryService.RegisterExitByQrAsync(qrCode);

                if (success)
                {
                    TimeSpan duration = DateTime.UtcNow - session.entry_time;
                    decimal hoursToCharge = (decimal)Math.Ceiling(duration.TotalHours);
                    if (hoursToCharge < 1) hoursToCharge = 1;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        AmountToCharge = hoursToCharge * 1.00m;
                        PlateNumber = session.plate;
                        StatusMessage = $"✅ SALIDA POR QR: {session.plate} | Total: {AmountToCharge:C2}";
                    });

                    await LoadSlotStatsAsync();
                    _parkingStatusNotifier.NotifyParkingStatusChanged();
                }
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => _dialogService.ShowError("Error", $"Error en salida QR: {ex.Message}"));
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
