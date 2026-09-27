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

        private CancellationTokenSource? _previewCancellation;

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
        private string _pendingPlate = string.Empty;
        private DateTime _pendingPlateTime = DateTime.MinValue;
        private readonly SemaphoreSlim _sessionOperationGate = new(1, 1);
        private string _handledPlate = string.Empty;
        private DateTime _handledPlateLastSeenUtc = DateTime.MinValue;
        private static readonly TimeSpan PlateReentryInterval = TimeSpan.FromSeconds(5);
        private string _lastExitedPlate = string.Empty;
        private DateTime _lastExitUtc = DateTime.MinValue;
        private static readonly TimeSpan ExitEntryCooldown = TimeSpan.FromSeconds(30);

        private readonly IQrService _qrService;
        private int _qrDetectionInProgress;
        private DateTime _lastQrDetectionUtc = DateTime.MinValue;
        private DateTime _lastQrVisibleUtc = DateTime.MinValue;
        private string _lastScannedQr = string.Empty;
        private DateTime _lastQrScanTime = DateTime.MinValue;
        private static readonly TimeSpan QrDetectionInterval = TimeSpan.FromMilliseconds(350);

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
        public ObservableCollection<parking_slot> AvailableSlots { get; } = new ObservableCollection<parking_slot>();

        private parking_slot? _selectedSlot;
        public parking_slot? SelectedSlot
        {
            get => _selectedSlot;
            set => SetProperty(ref _selectedSlot, value);
        }

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
        public ICommand UseAutomaticSlotCommand { get; }

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
            UseAutomaticSlotCommand = new RelayCommand(_ => SelectedSlot = null);

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await StartCameraAsync();
                // Las consultas comparten el mismo DbContext; van en secuencia,
                // pero no retrasan la apertura del visor.
                await LoadVehicleTypesAsync();
                await LoadSlotStatsAsync();
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

                int? selectedId = SelectedSlot?.id;
                AvailableSlots.Clear();
                foreach (var slot in slots.Where(s => !s.is_occupied))
                    AvailableSlots.Add(slot);
                SelectedSlot = AvailableSlots.FirstOrDefault(s => s.id == selectedId);
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
            string plate = PlateNumber.Trim().ToUpperInvariant();
            if (string.Equals(plate, _lastExitedPlate, StringComparison.OrdinalIgnoreCase)
                && DateTime.UtcNow - _lastExitUtc < ExitEntryCooldown)
            {
                _dialogService.ShowWarning("Salida reciente",
                    $"La salida de {plate} acaba de registrarse. Espere a que el vehículo despeje la puerta antes de iniciar otra entrada.");
                return;
            }
            if (SelectedVehicleType == null)
            {
                _dialogService.ShowWarning("Atención", "Seleccione un tipo de vehículo.");
                return;
            }

            try
            {
                AmountToCharge = 0;
                // La captura pertenece a la última detección visible; una corrección manual
                // conserva esa imagen mientras siga siendo reciente.
                byte[]? plateImage = _currentDetection?.PlateImage.Length > 0
                    && DateTime.UtcNow - _lastDetectionTime < TimeSpan.FromSeconds(30)
                    ? _currentDetection.PlateImage : null;
                string? assignedSlot;
                await _sessionOperationGate.WaitAsync();
                try
                {
                    assignedSlot = await _entryService.RegisterEntryAsync(
                        plate, SelectedVehicleType.id, plateImage, SelectedSlot?.id);
                    if (!string.IsNullOrEmpty(assignedSlot) && assignedSlot != "EXISTENTE")
                    {
                        // El mismo vehículo puede seguir delante de la cámara tras
                        // registrar la entrada. Solo una nueva aparición causa salida.
                        MarkPlateHandled(plate);
                        await LoadSlotStatsAsync();
                    }
                }
                finally { _sessionOperationGate.Release(); }

                if (assignedSlot == "EXISTENTE")
                {
                    _dialogService.ShowInfo("Información", $"El vehículo {PlateNumber} ya tiene una sesión activa.");
                }
                else if (!string.IsNullOrEmpty(assignedSlot))
                {
                    StatusMessage = $"ENTRADA: {plate} asignado al puesto {assignedSlot}.";
                    ScannerAudioFeedback.PlayEntry();
                    _dialogService.ShowSuccess("¡Éxito!", $"ENTRADA: {PlateNumber} asignado al puesto {assignedSlot}.");
                    _parkingStatusNotifier.NotifyParkingStatusChanged();
                }
                else
                {
                    _dialogService.ShowWarning("Sin puestos", "No hay puestos libres para registrar la entrada.");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error", ex.Message);
                await _sessionOperationGate.WaitAsync();
                try { await LoadSlotStatsAsync(); }
                finally { _sessionOperationGate.Release(); }
            }
        }

        private async Task RegisterExitAsync()
        {
            if (string.IsNullOrWhiteSpace(PlateNumber)) return;
            await RegisterExitForIdentifierAsync(PlateNumber.Trim().ToUpperInvariant(), isQr: false, showMissingSession: true);
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
                        var visibleDetection = DateTime.UtcNow - _lastDetectionTime < DetectionDisplayTime
                            ? _currentDetection : null;
                        byte[] frameToShow = DrawDetectionOnFrame(currentFrame, visibleDetection);

                        var preview = BuildBitmapSource(frameToShow);
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => CameraPreview = preview);
                        TryQueueQrDetection(currentFrame);
                        TryQueuePlateDetection(currentFrame);
                    }
                    // Read() ya espera al siguiente fotograma. Solo hacemos una
                    // pausa breve si el dispositivo aún no entrega imágenes.
                    if (currentFrame.Length == 0)
                        await Task.Delay(50, cancellationToken);
                }
            }
            catch { }
        }

        private void TryQueueQrDetection(byte[] currentFrame)
        {
            if (currentFrame.Length == 0 || DateTime.UtcNow - _lastQrDetectionUtc < QrDetectionInterval) return;
            if (Interlocked.CompareExchange(ref _qrDetectionInProgress, 1, 0) != 0) return;

            _lastQrDetectionUtc = DateTime.UtcNow;
            _ = ProcessQrDetectionAsync(currentFrame);
        }

        private async Task ProcessQrDetectionAsync(byte[] frame)
        {
            try
            {
                string qrText = await _qrService.ReadQrAsync(frame);
                if (!string.IsNullOrWhiteSpace(qrText))
                {
                    DateTime now = DateTime.UtcNow;
                    _lastQrVisibleUtc = now;
                    if (string.Equals(_lastScannedQr, qrText, StringComparison.OrdinalIgnoreCase)
                        && now - _lastQrScanTime < TimeSpan.FromSeconds(5))
                    {
                        // Mientras el ticket siga ante la cámara, una sola lectura
                        // basta. La siguiente se permite después de retirarlo.
                        _lastQrScanTime = now;
                        return;
                    }

                    _lastScannedQr = qrText;
                    _lastQrScanTime = now;

                    if (!qrText.StartsWith("SESSION-", StringComparison.OrdinalIgnoreCase))
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            StatusMessage = Uri.TryCreate(qrText, UriKind.Absolute, out _)
                                ? "El QR contiene un enlace web. El ticket debe codificar directamente el valor qr_data de la sesión."
                                : "El QR no contiene un código de sesión válido.");
                        return;
                    }

                    await RegisterExitForIdentifierAsync(qrText, isQr: true, showMissingSession: false);
                }
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    StatusMessage = $"Error al leer el QR: {ex.Message}");
            }
            finally { Interlocked.Exchange(ref _qrDetectionInProgress, 0); }
        }

        private async Task<bool> RegisterExitForIdentifierAsync(string identifier, bool isQr, bool showMissingSession)
        {
            try
            {
                parking_session? session;
                bool success;
                await _sessionOperationGate.WaitAsync();
                try
                {
                    session = isQr
                        ? await _entryService.GetActiveSessionByQrAsync(identifier)
                        : await _entryService.GetActiveSessionByPlateAsync(identifier);

                    success = session != null && (isQr
                        ? await _entryService.RegisterExitByQrAsync(identifier)
                        : await _entryService.RegisterExitByPlateAsync(identifier));

                    if (success)
                    {
                        MarkPlateHandled(session!.plate);
                        _lastExitedPlate = session.plate;
                        _lastExitUtc = DateTime.UtcNow;
                        _lastScannedQr = session.qr_data ?? string.Empty;
                        _lastQrScanTime = DateTime.UtcNow;
                        await LoadSlotStatsAsync();
                    }
                }
                finally { _sessionOperationGate.Release(); }

                if (session == null)
                {
                    if (showMissingSession)
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AmountToCharge = 0;
                            _dialogService.ShowWarning("Atención", $"No hay sesión activa para {identifier}.");
                        });
                    else if (isQr)
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = "QR inválido o sesión ya cerrada.");
                    return false;
                }

                if (!success)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        _dialogService.ShowError("Error", $"No se pudo registrar la salida de {session.plate}."));
                    return false;
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AmountToCharge = session.amount_due ?? 0;
                    // El campo de entrada queda vacío. Una lectura de la placa
                    // trasera al salir no prepara otra entrada accidentalmente.
                    PlateNumber = string.Empty;
                    _currentDetection = null;
                    _pendingPlate = string.Empty;
                    StatusMessage = $"SALIDA POR {(isQr ? "QR" : "PLACA")}: {session.plate} | Total: {AmountToCharge:C2}";
                    ScannerAudioFeedback.PlayExit();
                    _dialogService.ShowSuccess("¡Éxito!", $"SALIDA: {session.plate} | Total: {AmountToCharge:C2}");
                });
                _parkingStatusNotifier.NotifyParkingStatusChanged();
                return true;
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    _dialogService.ShowError("Error", $"Error en salida: {ex.Message}"));
                return false;
            }
        }

        private void TryQueuePlateDetection(byte[] currentFrame)
        {
            if (currentFrame.Length == 0 || DateTime.UtcNow - _lastAutoDetectionUtc < AutoDetectionInterval) return;
            // Un ticket ante la cámara tiene prioridad sobre el OCR de placas.
            if (DateTime.UtcNow - _lastQrVisibleUtc < TimeSpan.FromSeconds(2)) return;
            if (Interlocked.CompareExchange(ref _autoDetectionInProgress, 1, 0) != 0) return;

            _lastAutoDetectionUtc = DateTime.UtcNow;
            _ = ProcessAutomaticDetectionAsync(currentFrame);
        }

        private async Task ProcessAutomaticDetectionAsync(byte[] frame)
        {
            try
            {
                var detection = await ((PlateReaderService)_plateService).DetectPlateWithRegionsAsync(frame);
                if (detection.HasPlateCandidates && string.IsNullOrWhiteSpace(detection.PlateNumber)
                    && !string.IsNullOrEmpty(_handledPlate))
                {
                    // El OCR puede fallar unos frames aunque el vehículo continúe
                    // frente a la cámara; no lo tratamos como una nueva llegada.
                    _handledPlateLastSeenUtc = DateTime.UtcNow;
                }
                if (!string.IsNullOrWhiteSpace(detection.PlateNumber))
                {
                    // Dos lecturas iguales en frames distintos reducen las placas falsas por reflejos.
                    if (detection.PlateNumber != _pendingPlate || DateTime.UtcNow - _pendingPlateTime > TimeSpan.FromSeconds(12))
                    {
                        _pendingPlate = detection.PlateNumber;
                        _pendingPlateTime = DateTime.UtcNow;
                        return;
                    }
                    _currentDetection = detection;
                    _lastDetectionTime = DateTime.UtcNow;
                    if (ShouldIgnoreRepeatedPlate(detection.PlateNumber)) return;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        PlateNumber = detection.PlateNumber;
                        StatusMessage = $"Placa reconocida: {detection.PlateNumber}. Registre la entrada o escanee el QR para la salida.";
                    });

                    // En la misma puerta no se puede inferir la dirección del
                    // vehículo a partir de la placa. La cámara solo rellena el
                    // campo; la salida automática requiere el QR del ticket.
                    MarkPlateHandled(detection.PlateNumber);
                }
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    StatusMessage = $"Error al reconocer la placa: {ex.Message}");
            }
            finally { Interlocked.Exchange(ref _autoDetectionInProgress, 0); }
        }

        private void MarkPlateHandled(string plate)
        {
            _handledPlate = plate;
            _handledPlateLastSeenUtc = DateTime.UtcNow;
        }

        private bool ShouldIgnoreRepeatedPlate(string plate)
        {
            if (!string.Equals(_handledPlate, plate, StringComparison.OrdinalIgnoreCase))
                return false;

            DateTime now = DateTime.UtcNow;
            if (now - _handledPlateLastSeenUtc >= PlateReentryInterval)
                return false;

            // La misma placa debe desaparecer unos segundos antes de que una
            // nueva lectura se interprete como otro paso por la cámara.
            _handledPlateLastSeenUtc = now;
            return true;
        }

        private byte[] DrawDetectionOnFrame(byte[] frameBytes, PlateDetectionResult? detection)
        {
            using var mat = Cv2.ImDecode(frameBytes, ImreadModes.Color);
            var roi = PlateReaderService.GetRecognitionRegion(mat.Width, mat.Height);
            Cv2.Rectangle(mat, roi, Scalar.Cyan, 2, LineTypes.AntiAlias);
            if (detection?.HasDetection == true)
            {
                var rect = detection.DetectedRegions[0];
                Cv2.Rectangle(mat, rect, Scalar.LimeGreen, 4, LineTypes.AntiAlias);
                if (!string.IsNullOrWhiteSpace(detection.PlateNumber))
                    Cv2.PutText(mat, detection.PlateNumber,
                        new OpenCvSharp.Point(rect.X, Math.Max(20, rect.Y - 15)),
                        HersheyFonts.HersheySimplex, 1.2, Scalar.LimeGreen, 3);
            }
            return mat.ToBytes(".jpg");
        }


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
