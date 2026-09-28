using OpenCvSharp;
using Parking.Application.EntityService;
using Parking.Application.Services;
using Parking.Application.UseCases;
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

        private readonly ICameraSourceCatalog _cameraSourceCatalog;
        private readonly CameraSelectionStore _cameraSelectionStore;
        private readonly CameraSelectionConfiguration? _savedCameraSelection;
        private readonly IPlateService _plateService;
        private readonly IEntryService _entryService;
        private readonly IParkingStatusNotifier _parkingStatusNotifier;

        // Detector compartido: una lectura OCR a la vez protege el modelo nativo.
        private readonly SemaphoreSlim _plateReaderGate = new(1, 1);
        // Apertura y cierre se serializan para que una captura no se libere durante su inicio.
        private readonly SemaphoreSlim _cameraOperationGate = new(1, 1);
        private bool _isOperational = true;

        private string _plateNumber = string.Empty;
        private string _statusMessage = "Listo para iniciar.";
        private decimal _amountToCharge = 0;
        /// <summary>Fuentes disponibles para los dos visores.</summary>
        public ObservableCollection<CameraSource> CameraSources { get; } = new();
        /// <summary>Visor orientado a la zona de ingreso.</summary>
        public CameraFeedViewModel EntranceFeed { get; }
        /// <summary>Visor orientado a la zona de salida o lectura de tickets.</summary>
        public CameraFeedViewModel ExitFeed { get; }

        // Propiedades para las tarjetas superiores
        private int _totalSlots;
        private int _occupiedSlots;
        private int _freeSlots;

        public int TotalSlots { get => _totalSlots; set => SetProperty(ref _totalSlots, value); }
        public int OccupiedSlots { get => _occupiedSlots; set => SetProperty(ref _occupiedSlots, value); }
        public int FreeSlots { get => _freeSlots; set => SetProperty(ref _freeSlots, value); }

        private PlateDetectionResult? _currentDetection;
        private DateTime _lastDetectionTime = DateTime.MinValue;
        // El mismo contexto de datos atiende entrada y salida; esta puerta evita operaciones simultáneas.
        private readonly SemaphoreSlim _sessionOperationGate = new(1, 1);
        private string _handledPlate = string.Empty;
        private DateTime _handledPlateLastSeenUtc = DateTime.MinValue;
        // La placa debe desaparecer antes de aceptar otra lectura como nuevo vehículo.
        private static readonly TimeSpan PlateReentryInterval = TimeSpan.FromSeconds(5);
        private string _lastExitedPlate = string.Empty;
        private DateTime _lastExitUtc = DateTime.MinValue;
        // Tras una salida, el vehículo dispone de tiempo para despejar la cámara.
        private static readonly TimeSpan ExitEntryCooldown = TimeSpan.FromSeconds(30);

        private readonly IQrService _qrService;
        private string _lastScannedQr = string.Empty;
        private DateTime _lastQrScanTime = DateTime.MinValue;
        // Espaciado mínimo de lecturas QR para no procesar cada fotograma.
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
        /// <summary>Indica si al menos uno de los dos visores está capturando.</summary>
        public bool IsCameraRunning => EntranceFeed.IsRunning || ExitFeed.IsRunning;

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
        /// <summary>Inicia o cambia únicamente el visor indicado.</summary>
        public ICommand StartFeedCommand { get; }
        /// <summary>Detiene únicamente el visor indicado y libera su dispositivo.</summary>
        public ICommand StopFeedCommand { get; }
        public ICommand RefreshCamerasCommand { get; }
        public ICommand SavePlateCommand { get; }
        public ICommand RegisterExitCommand { get; }
        public ICommand SelectVehicleTypeCommand { get; }
        public ICommand UseAutomaticSlotCommand { get; }

        /// <summary>Conecta cámara, reconocimiento, sesiones y catálogos usados en la operación de entrada y salida.</summary>
        /// <param name="cameraFactory">Crea una captura distinta para cada visor.</param>
        /// <param name="cameraSourceCatalog">Busca cámaras conectadas a Windows.</param>
        /// <param name="cameraSelectionStore">Recupera y protege las selecciones de fuentes.</param>
        /// <param name="plateService">Reconoce placas dentro del fotograma.</param>
        /// <param name="qrService">Lee los tickets QR mostrados a la cámara.</param>
        /// <param name="entryService">Registra y consulta las sesiones de estacionamiento.</param>
        /// <param name="vehicleTypeRepository">Proporciona tipos de vehículo disponibles.</param>
        /// <param name="slotRepo">Consulta la ocupación y los puestos libres.</param>
        /// <param name="parkingStatusNotifier">Notifica cambios de ocupación a otras vistas.</param>
        /// <param name="dialogService">Presenta avisos y errores al operador.</param>
        public PlateReaderViewModel(
            ICameraServiceFactory cameraFactory,
            ICameraSourceCatalog cameraSourceCatalog,
            CameraSelectionStore cameraSelectionStore,
            IPlateService plateService,
            IQrService qrService,
            IEntryService entryService,
            Ivehicle_typeRepository vehicleTypeRepository,
            IParkingSlotRepository slotRepo,
            IParkingStatusNotifier parkingStatusNotifier,
            IDialogService dialogService) 
        {
            _dialogService = dialogService;
            _cameraSourceCatalog = cameraSourceCatalog;
            _cameraSelectionStore = cameraSelectionStore;
            // La última URL se recupera antes de la enumeración de dispositivos.
            _savedCameraSelection = _cameraSelectionStore.Load();
            // Ambos paneles comparten opciones, pero cada uno posee un VideoCapture nuevo.
            EntranceFeed = new CameraFeedViewModel("ENTRADA", CameraSources, cameraFactory);
            ExitFeed = new CameraFeedViewModel("SALIDA / QR", CameraSources, cameraFactory);
            EntranceFeed.GenericNetworkUrl = _savedCameraSelection?.EntranceUrl ?? string.Empty;
            ExitFeed.GenericNetworkUrl = _savedCameraSelection?.ExitUrl ?? string.Empty;
            EntranceFeed.DroidCamNetworkUrl = _savedCameraSelection?.EntranceDroidCamUrl ?? string.Empty;
            ExitFeed.DroidCamNetworkUrl = _savedCameraSelection?.ExitDroidCamUrl ?? string.Empty;
            _plateService = plateService;
            _entryService = entryService;
            _qrService = qrService;
            _vehicleTypeRepository = vehicleTypeRepository;
            _slotRepo = slotRepo;
            _parkingStatusNotifier = parkingStatusNotifier;

            StartCameraCommand = new AsyncRelayCommand(_ => StartCameraAsync());
            StopCameraCommand = new AsyncRelayCommand(_ => StopCameraAsync());
            StartFeedCommand = new AsyncRelayCommand(parameter =>
                parameter is CameraFeedViewModel feed ? StartSingleFeedAsync(feed) : Task.CompletedTask);
            StopFeedCommand = new AsyncRelayCommand(parameter =>
                parameter is CameraFeedViewModel feed ? StopSingleFeedAsync(feed) : Task.CompletedTask);
            RefreshCamerasCommand = new AsyncRelayCommand(_ => RefreshCamerasAsync());
            SavePlateCommand = new AsyncRelayCommand(_ => SaveCorrectedPlateAsync());
            RegisterExitCommand = new AsyncRelayCommand(_ => RegisterExitAsync());
            SelectVehicleTypeCommand = new RelayCommand(param =>
            {
                if (param is vehicle_type selectedType) SelectedVehicleType = selectedType;
            });
            UseAutomaticSlotCommand = new RelayCommand(_ => SelectedSlot = null);

            _ = InitializeAsync();
        }

        /// <summary>Descubre fuentes y carga los catálogos sin encender una cámara en un visor no solicitado.</summary>
        /// <returns>Tarea de preparación inicial de la pantalla.</returns>
        private async Task InitializeAsync()
        {
            try
            {
                // Detectamos dispositivos sin abrirlos de forma permanente.
                await RefreshCamerasAsync();
                // El cierre de la aplicación puede comenzar mientras se buscan drivers.
                if (!_isOperational) return;
                // Se espera la orden explícita del operador para cada cámara.
                EntranceFeed.Status = "Seleccione una cámara y pulse Iniciar entrada.";
                ExitFeed.Status = "Seleccione una cámara y pulse Iniciar salida.";
                // Las consultas comparten el mismo DbContext; van en secuencia,
                // mientras el operador decide qué fuente iniciar en cada visor.
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

        /// <summary>
        /// Envía la placa al servicio de entrada y muestra al operador si la
        /// sesión quedó como mensualizada, ocasional o con cuota pendiente.
        /// </summary>
        /// <returns>Una tarea que termina tras registrar la entrada o mostrar el error.</returns>
        private async Task SaveCorrectedPlateAsync()
        {
            // Una entrada necesita al menos una placa visible o escrita.
            if (string.IsNullOrWhiteSpace(PlateNumber)) return;
            // El mismo formato de placa se usa en las búsquedas y los mensajes.
            string plate = PlateNumber.Trim().ToUpperInvariant();
            if (string.Equals(plate, _lastExitedPlate, StringComparison.OrdinalIgnoreCase)
                && DateTime.UtcNow - _lastExitUtc < ExitEntryCooldown)
            {
                _dialogService.ShowWarning("Salida reciente",
                    $"La salida de {plate} acaba de registrarse. Espere a que el vehículo despeje la puerta antes de iniciar otra entrada.");
                return;
            }
            try
            {
                // Borra el importe mostrado por una salida anterior.
                AmountToCharge = 0;
                // La captura pertenece a la última detección visible; una corrección manual
                // conserva esa imagen mientras siga siendo reciente.
                byte[]? plateImage = _currentDetection?.PlateImage.Length > 0
                    && DateTime.UtcNow - _lastDetectionTime < TimeSpan.FromSeconds(30)
                    ? _currentDetection.PlateImage : null;
                // El resultado contiene puesto, modalidad y cuota pendiente.
                EntryRegistrationResult entry;
                // Las operaciones sobre el DbContext compartido van una por vez.
                await _sessionOperationGate.WaitAsync();
                try
                {
                    // Un cliente registrado usa su tipo guardado. Si no hay tipo
                    // elegido, el servicio solo rechazará una placa ocasional.
                    entry = await _entryService.RegisterEntryDetailedAsync(
                        plate, SelectedVehicleType?.id ?? 0, plateImage, SelectedSlot?.id);
                    // EXISTENTE y nulo son resultados especiales, no puestos.
                    string? assignedSlot = entry.SlotNumber;
                    if (!string.IsNullOrEmpty(assignedSlot) && assignedSlot != "EXISTENTE")
                    {
                        // El mismo vehículo puede seguir delante de la cámara tras
                        // registrar la entrada. Solo una nueva aparición causa salida.
                        MarkPlateHandled(plate);
                        await LoadSlotStatsAsync();
                    }
                }
                finally { _sessionOperationGate.Release(); }

                // Se informa primero una entrada duplicada; no se crea otro ticket.
                if (entry.SlotNumber == "EXISTENTE")
                {
                    _dialogService.ShowInfo("Información", $"El vehículo {PlateNumber} ya tiene una sesión activa.");
                }
                else if (!string.IsNullOrEmpty(entry.SlotNumber))
                {
                    // Cada clasificación explica al operador si hubo ticket o
                    // si se respetó el acceso mensual sin cargo por estancia.
                    string accessMessage = entry.Access.Kind switch
                    {
                        MonthlyAccessKind.Monthly => "Cliente mensualizado: entrada registrada sin ticket y sin cobro de estancia.",
                        MonthlyAccessKind.OutsideSchedule => "Fuera del horario mensual. Se aplicará la tarifa ocasional.",
                        MonthlyAccessKind.Expired => "Contrato vencido. Se aplicará la tarifa ocasional.",
                        MonthlyAccessKind.Inactive => "Contrato inactivo. Se aplicará la tarifa ocasional.",
                        MonthlyAccessKind.NotStarted => "El contrato aún no inicia. Se aplicará la tarifa ocasional.",
                        _ => "Se aplicará la tarifa ocasional."
                    };
                    // La cuota vencida se muestra aparte del cobro ocasional.
                    if (entry.Access.PendingFee > 0)
                        accessMessage += $" Cuota mensual pendiente: {entry.Access.PendingFee:C2}.";
                    // El puesto y la regla aplicada quedan visibles en pantalla.
                    StatusMessage = $"ENTRADA: {plate} asignado al puesto {entry.SlotNumber}. {accessMessage}";
                    ScannerAudioFeedback.PlayEntry();
                    // Una deuda se destaca como aviso aun cuando la entrada se guardó.
                    if (entry.Access.PendingFee > 0)
                        _dialogService.ShowWarning("Entrada registrada: cuota pendiente", StatusMessage);
                    else
                        _dialogService.ShowSuccess("Entrada registrada", StatusMessage);
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

        /// <summary>Inicia la salida manual por placa, incluida la de clientes sin ticket.</summary>
        /// <returns>Una tarea que termina tras intentar cerrar la sesión.</returns>
        private async Task RegisterExitAsync()
        {
            // El botón manual siempre busca por placa y muestra si no hay sesión.
            if (string.IsNullOrWhiteSpace(PlateNumber)) return;
            await RegisterExitForIdentifierAsync(PlateNumber.Trim().ToUpperInvariant(), isQr: false, showMissingSession: true);
        }

        /// <summary>Vuelve a buscar cámaras Windows y conserva las selecciones todavía disponibles.</summary>
        /// <returns>Tarea que termina tras actualizar las opciones de ambos visores.</returns>
        private async Task RefreshCamerasAsync()
        {
            // El bloqueo impide abrir una fuente mientras se están sondeando índices.
            await _cameraOperationGate.WaitAsync();
            try
            {
                // Un dispositivo abierto se libera antes de consultar el catálogo.
                bool restartEntrance = EntranceFeed.IsRunning;
                bool restartExit = ExitFeed.IsRunning;
                await StopFeedAsync(EntranceFeed);
                await StopFeedAsync(ExitFeed);
                OnPropertyChanged(nameof(IsCameraRunning));
                // La selección guardada se usa solo en la primera búsqueda de esta vista.
                string? entranceId = EntranceFeed.SelectedSource?.Id ?? _savedCameraSelection?.EntranceSourceId;
                string? exitId = ExitFeed.SelectedSource?.Id ?? _savedCameraSelection?.ExitSourceId;
                try
                {
                    // El catálogo prueba los índices sin dejar cámaras ocupadas.
                    var discovered = await _cameraSourceCatalog.DiscoverAsync();
                    CameraSources.Clear();
                    foreach (var source in discovered) CameraSources.Add(source);
                    // La opción genérica admite cualquier vídeo RTSP o HTTP compatible.
                    CameraSources.Add(new CameraSource("network", "Cámara IP / red (RTSP/HTTP)",
                        CameraSourceKind.NetworkStream));
                    // DroidCam conserva su abreviatura sin imponerla a otras cámaras.
                    CameraSources.Add(new CameraSource("network:droidcam", "DroidCam Wi-Fi (opcional)",
                        CameraSourceKind.NetworkStream, AddressProfile: CameraAddressProfile.DroidCam));

                    // Las selecciones previas se recuperan por Id y no por objeto antiguo.
                    EntranceFeed.SelectedSource = CameraSources.FirstOrDefault(x => x.Id == entranceId)
                        ?? discovered.FirstOrDefault();
                    ExitFeed.SelectedSource = CameraSources.FirstOrDefault(x => x.Id == exitId)
                        ?? discovered.FirstOrDefault(x => x.Id != EntranceFeed.SelectedSource?.Id);

                    // Un solo dispositivo deja libre el segundo panel para un flujo de red.
                    if (discovered.Count == 0)
                        StatusMessage = "No se detectaron cámaras Windows. Puede introducir una URL RTSP/HTTP.";
                }
                catch (Exception ex)
                {
                    // El fallo de enumeración no impide configurar una fuente de red.
                    CameraSources.Clear();
                    CameraSources.Add(new CameraSource("network", "Cámara IP / red (RTSP/HTTP)",
                        CameraSourceKind.NetworkStream));
                    CameraSources.Add(new CameraSource("network:droidcam", "DroidCam Wi-Fi (opcional)",
                        CameraSourceKind.NetworkStream, AddressProfile: CameraAddressProfile.DroidCam));
                    StatusMessage = $"No se pudieron buscar cámaras: {ex.Message}";
                }

                // Se reabren únicamente los visores que estaban activos antes de Buscar.
                if (_isOperational)
                {
                    if (restartEntrance) await StartFeedAsync(EntranceFeed);
                    if (restartExit) await StartFeedAsync(ExitFeed);
                    OnPropertyChanged(nameof(IsCameraRunning));
                }
            }
            finally { _cameraOperationGate.Release(); }
        }

        /// <summary>Detiene las capturas al cerrar la ventana principal.</summary>
        /// <returns>Tarea que termina cuando ambas fuentes quedan liberadas.</returns>
        public Task DeactivateAsync()
        {
            // Evita que una enumeración aún pendiente vuelva a abrir cámaras.
            _isOperational = false;
            return StopCameraAsync();
        }

        /// <summary>Abre hasta dos fuentes seleccionadas y comienza sus bucles independientes.</summary>
        /// <returns>Tarea que termina tras intentar conectar ambos visores.</returns>
        private async Task StartCameraAsync()
        {
            // Una aplicación en cierre no debe volver a abrir capturas.
            if (!_isOperational) return;
            await _cameraOperationGate.WaitAsync();
            try
            {
                // Reconfigurar primero libera los dispositivos asignados antes de abrir otros.
                await StopFeedAsync(EntranceFeed);
                await StopFeedAsync(ExitFeed);
                await StartSelectedFeedsAsync();
                // Una búsqueda sin fuente funcional no reemplaza la preferencia anterior.
                if (IsCameraRunning)
                    SaveCameraSelection();
            }
            finally { _cameraOperationGate.Release(); }
        }

        /// <summary>Abre las dos selecciones evitando asignar el mismo dispositivo local dos veces.</summary>
        /// <returns>Tarea que termina tras intentar conectar cada panel.</returns>
        private async Task StartSelectedFeedsAsync()
        {
            // La primera selección se abre con independencia del estado de la segunda.
            await StartFeedAsync(EntranceFeed);

            // Una cámara local solo puede tener un propietario en esta pantalla.
            bool sameDevice = EntranceFeed.IsRunning
                && EntranceFeed.ActiveSource?.Kind == CameraSourceKind.WindowsDevice
                && EntranceFeed.ActiveSource.Id == ExitFeed.SelectedSource?.Id;
            if (sameDevice)
                ExitFeed.Status = "La cámara está en Entrada. Pulse Iniciar aquí para trasladarla a Salida.";
            else
                await StartFeedAsync(ExitFeed);

            OnPropertyChanged(nameof(IsCameraRunning));
        }

        /// <summary>Valida la fuente del panel y abre su captura sin afectar al otro panel.</summary>
        /// <param name="feed">Visor que se va a iniciar.</param>
        /// <returns>Tarea de apertura y preparación del bucle de imágenes.</returns>
        private async Task StartFeedAsync(CameraFeedViewModel feed)
        {
            // Un panel sin elección permanece disponible para configuración posterior.
            var selected = feed.SelectedSource;
            if (selected == null)
            {
                feed.Status = "Seleccione una cámara.";
                return;
            }

            // La URL genérica se conserva; solo el perfil opcional completa DroidCam.
            CameraSource source = selected;
            if (selected.Kind == CameraSourceKind.NetworkStream)
            {
                if (!CameraStreamAddress.TryNormalize(feed.NetworkUrl, selected.AddressProfile, out string url))
                {
                    feed.Status = selected.AddressProfile == CameraAddressProfile.DroidCam
                        ? "Escriba la IP del teléfono o una URL de vídeo válida."
                        : "Escriba la URL completa RTSP/HTTP de la cámara.";
                    return;
                }
                // Mostramos la dirección completa que se intentará conectar.
                feed.NetworkUrl = url;
                source = selected with { StreamUrl = url };
            }

            try
            {
                // Cada captura pertenece exclusivamente a su panel.
                bool started = await feed.Capture.StartCameraAsync(source);
                if (!started)
                {
                    feed.Status = "No se pudo abrir esta fuente. Revise conexión y selección.";
                    return;
                }

                // El bucle conserva su propia cancelación y sus propias estadísticas OCR/QR.
                feed.Cancellation = new CancellationTokenSource();
                feed.ActiveSource = source;
                feed.IsRunning = true;
                feed.Status = "Imagen en directo. Lee placas y QR.";
                feed.PreviewTask = RunPreviewLoopAsync(feed, feed.Cancellation.Token);
            }
            catch (Exception)
            {
                // No se presenta la excepción: una URL podría incluir credenciales.
                await feed.Capture.StopCameraAsync();
                feed.ActiveSource = null;
                feed.IsRunning = false;
                feed.Status = "No se pudo conectar esta cámara. Revise la fuente.";
            }
        }

        /// <summary>Abre un visor y traslada a él la cámara local si el otro la está usando.</summary>
        /// <param name="feed">Visor de destino seleccionado por el operador.</param>
        /// <returns>Tarea que termina cuando la captura anterior se libera y la nueva se abre.</returns>
        private async Task StartSingleFeedAsync(CameraFeedViewModel feed)
        {
            // Una aplicación en cierre no debe volver a encender el dispositivo.
            if (!_isOperational) return;
            await _cameraOperationGate.WaitAsync();
            try
            {
                // Detenemos la captura previa de este panel antes de aplicar su nueva selección.
                await StopFeedAsync(feed);
                if (feed.Capture.IsCameraRunning)
                {
                    // No se abre otra fuente sobre un driver que no confirmó el cierre.
                    feed.Status = "La cámara sigue ocupada. Intente detenerla de nuevo.";
                    OnPropertyChanged(nameof(IsCameraRunning));
                    return;
                }
                var other = ReferenceEquals(feed, EntranceFeed) ? ExitFeed : EntranceFeed;
                // La cámara física solo se traslada si el otro visor la mantiene abierta.
                if (feed.SelectedSource?.Kind == CameraSourceKind.WindowsDevice
                    && other.ActiveSource?.Kind == CameraSourceKind.WindowsDevice
                    && other.ActiveSource.Id == feed.SelectedSource.Id)
                {
                    await StopFeedAsync(other);
                    // Un controlador que continúa ocupado no debe recibir una segunda apertura.
                    if (other.Capture.IsCameraRunning)
                    {
                        feed.Status = "La otra cámara sigue ocupada. Intente detenerla de nuevo.";
                        OnPropertyChanged(nameof(IsCameraRunning));
                        return;
                    }
                    other.Status = $"Cámara trasladada a {feed.Title}.";
                }

                // La apertura ocurre después del cierre nativo del dispositivo anterior.
                await StartFeedAsync(feed);
                OnPropertyChanged(nameof(IsCameraRunning));
                if (feed.IsRunning) SaveCameraSelection();
            }
            finally { _cameraOperationGate.Release(); }
        }

        /// <summary>Cierra un visor sin interrumpir la captura del otro.</summary>
        /// <param name="feed">Visor cuya cámara se debe liberar.</param>
        /// <returns>Tarea que termina después del cierre del dispositivo.</returns>
        private async Task StopSingleFeedAsync(CameraFeedViewModel feed)
        {
            await _cameraOperationGate.WaitAsync();
            try
            {
                // El mismo bloqueo coordina este cierre con los inicios y búsquedas.
                await StopFeedAsync(feed);
                OnPropertyChanged(nameof(IsCameraRunning));
            }
            finally { _cameraOperationGate.Release(); }
        }

        /// <summary>Guarda las dos elecciones después de conectar un visor.</summary>
        private void SaveCameraSelection()
        {
            try
            {
                // Persistimos índices y URLs para restaurarlos en la próxima apertura.
                _cameraSelectionStore.Save(new CameraSelectionConfiguration(
                    EntranceFeed.SelectedSource?.Id, EntranceFeed.GenericNetworkUrl,
                    ExitFeed.SelectedSource?.Id, ExitFeed.GenericNetworkUrl,
                    EntranceFeed.DroidCamNetworkUrl, ExitFeed.DroidCamNetworkUrl));
            }
            catch (Exception)
            {
                // Un fallo de preferencias no debe cerrar la captura abierta.
                StatusMessage = "Cámara abierta; no se pudo guardar su selección para el próximo inicio.";
            }
        }

        /// <summary>Detiene ambos bucles y libera sus capturas.</summary>
        /// <returns>Tarea que termina cuando los dispositivos quedan disponibles.</returns>
        private async Task StopCameraAsync()
        {
            await _cameraOperationGate.WaitAsync();
            try
            {
                await StopFeedAsync(EntranceFeed);
                await StopFeedAsync(ExitFeed);
                OnPropertyChanged(nameof(IsCameraRunning));
            }
            finally { _cameraOperationGate.Release(); }
        }

        /// <summary>Detiene un único visor antes de cerrar su captura OpenCV.</summary>
        /// <param name="feed">Visor que se va a liberar.</param>
        /// <returns>Tarea de cancelación y cierre.</returns>
        private static async Task StopFeedAsync(CameraFeedViewModel feed)
        {
            // Se espera el fin del bucle para no leer a la vez que se libera el driver.
            feed.Cancellation?.Cancel();
            if (feed.PreviewTask != null)
            {
                try { await feed.PreviewTask; }
                catch (Exception)
                {
                    // Se continúa con la liberación aunque el driver fallara al leer.
                }
            }
            feed.PreviewTask = null;
            feed.Cancellation?.Dispose();
            feed.Cancellation = null;
            try
            {
                await feed.Capture.StopCameraAsync();
            }
            catch (Exception)
            {
                // El estado nativo se consulta también si Release notificó un fallo.
            }
            bool released = !feed.Capture.IsCameraRunning;
            feed.Status = released ? "Cámara detenida y liberada." : "La cámara sigue ocupada; intente detenerla de nuevo.";
            feed.Preview = null;
            if (released) feed.ActiveSource = null;
            feed.IsRunning = !released;
        }

        /// <summary>Actualiza un visor y entrega sus fotogramas a los lectores QR y OCR.</summary>
        /// <param name="feed">Panel propietario de la captura y de la imagen.</param>
        /// <param name="cancellationToken">Detiene el bucle cuando se cierra la cámara.</param>
        /// <returns>Tarea continua de visualización hasta la cancelación.</returns>
        private async Task RunPreviewLoopAsync(CameraFeedViewModel feed, CancellationToken cancellationToken)
        {
            // Permite detectar una fuente abierta que deja de entregar imágenes.
            DateTime lastFrameUtc = DateTime.UtcNow;
            // El visor se limita a unas 15 actualizaciones por segundo por cámara.
            DateTime lastPreviewUtc = DateTime.MinValue;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Cada bucle lee solo de la fuente asignada a su panel.
                    // ConfigureAwait evita hacer decodificación y dibujo en el hilo de WPF.
                    byte[] currentFrame = await feed.Capture.CaptureFrameAsync().ConfigureAwait(false);
                    if (currentFrame.Length > 0)
                    {
                        DateTime now = DateTime.UtcNow;
                        lastFrameUtc = now;
                        if (now - lastPreviewUtc >= TimeSpan.FromMilliseconds(66))
                        {
                            // La caja OCR se dibuja únicamente en el visor que la detectó.
                            var visibleDetection = now - feed.LastDetectionUtc < DetectionDisplayTime
                                ? feed.CurrentDetection : null;
                            byte[] frameToShow = DrawDetectionOnFrame(currentFrame, visibleDetection);
                            // BitmapSource se congela para pasar con seguridad al hilo de WPF.
                            var preview = BuildBitmapSource(frameToShow);
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => feed.Preview = preview);
                            lastPreviewUtc = now;
                        }
                        // Los dos visores pueden leer ambas señales; sus límites son independientes.
                        TryQueueQrDetection(feed, currentFrame);
                        TryQueuePlateDetection(feed, currentFrame);
                    }
                    // Read() ya espera al siguiente fotograma. Solo hacemos una
                    // pausa breve si el dispositivo aún no entrega imágenes.
                    if (currentFrame.Length == 0)
                    {
                        // El operador puede buscar dispositivos y reiniciar la fuente perdida.
                        if (DateTime.UtcNow - lastFrameUtc > TimeSpan.FromSeconds(5))
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                feed.Status = "Esta cámara dejó de entregar imagen. Pulse Buscar o Iniciar.");
                            break;
                        }
                        await Task.Delay(50, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // Se conserva el otro visor en ejecución si esta fuente falla.
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    feed.Status = "Se interrumpió el vídeo de esta cámara.");
            }
            finally
            {
                // Una cancelación manual deja el cierre al método que espera este bucle.
                if (!cancellationToken.IsCancellationRequested)
                {
                    // Si la fuente terminó por error, este mismo bucle libera su driver.
                    try { await feed.Capture.StopCameraAsync(); }
                    catch (Exception)
                    {
                        // La otra fuente sigue operativa aunque este driver falle.
                    }
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        feed.Preview = null;
                        feed.ActiveSource = null;
                        feed.IsRunning = false;
                        OnPropertyChanged(nameof(IsCameraRunning));
                    });
                }
            }
        }

        /// <summary>Programa una lectura QR para un visor si su lector está disponible.</summary>
        /// <param name="feed">Panel que entregó el fotograma.</param>
        /// <param name="currentFrame">Fotograma actual de la cámara.</param>
        private void TryQueueQrDetection(CameraFeedViewModel feed, byte[] currentFrame)
        {
            // El intervalo y la bandera corresponden al panel, no a las dos cámaras.
            if (currentFrame.Length == 0 || DateTime.UtcNow - feed.LastQrDetectionUtc < QrDetectionInterval) return;
            if (Interlocked.CompareExchange(ref feed.QrDetectionInProgress, 1, 0) != 0) return;

            feed.LastQrDetectionUtc = DateTime.UtcNow;
            _ = ProcessQrDetectionAsync(feed, currentFrame);
        }

        /// <summary>Lee el QR, valida que identifique una sesión y registra su salida si corresponde.</summary>
        /// <param name="feed">Panel donde apareció el ticket.</param>
        /// <param name="frame">Fotograma de cámara que contiene el posible código.</param>
        /// <returns>Tarea de lectura y, cuando procede, de cierre de la sesión.</returns>
        private async Task ProcessQrDetectionAsync(CameraFeedViewModel feed, byte[] frame)
        {
            try
            {
                string qrText = await _qrService.ReadQrAsync(frame);
                if (!string.IsNullOrWhiteSpace(qrText))
                {
                    DateTime now = DateTime.UtcNow;
                    // Mientras haya un QR visible, el OCR de placas cede prioridad al ticket.
                    feed.LastQrVisibleUtc = now;
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

                    // Un enlace o texto arbitrario no representa el identificador de sesión.
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
            finally { Interlocked.Exchange(ref feed.QrDetectionInProgress, 0); }
        }

        /// <summary>Cierra una estancia por placa o QR y presenta el importe resultante.</summary>
        /// <param name="identifier">Placa o contenido del ticket leído en la salida.</param>
        /// <param name="isQr">Verdadero cuando el identificador procede de un ticket QR.</param>
        /// <param name="showMissingSession">Indica si debe abrirse un aviso cuando no haya sesión activa.</param>
        /// <returns>Verdadero si se cerró la sesión y se actualizó la ocupación visible.</returns>
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
                    // Ningún visor debe conservar una lectura previa a esta salida.
                    EntranceFeed.PendingPlate = string.Empty;
                    ExitFeed.PendingPlate = string.Empty;
                    // La marca se guardó al entrar: una edición posterior del plan
                    // no transforma esta salida mensual en cobro ocasional.
                    bool isMonthly = session.notes == MonthlyAccessPolicy.MonthlySessionNote;
                    // El mensualizado sale por placa y sin importe; el ocasional
                    // muestra el total calculado al cerrar la sesión.
                    StatusMessage = isMonthly
                        ? $"SALIDA POR PLACA: {session.plate} | Cliente mensualizado, sin cobro."
                        : $"SALIDA POR {(isQr ? "QR" : "PLACA")}: {session.plate} | Total: {AmountToCharge:C2}";
                    ScannerAudioFeedback.PlayExit();
                    _dialogService.ShowSuccess("¡Éxito!", StatusMessage);
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

        /// <summary>Programa OCR en un visor, con prioridad local para el QR de ese mismo visor.</summary>
        /// <param name="feed">Panel que entregó el fotograma.</param>
        /// <param name="currentFrame">Fotograma actual de la cámara.</param>
        private void TryQueuePlateDetection(CameraFeedViewModel feed, byte[] currentFrame)
        {
            // Cada panel puede buscar placas a su propio ritmo.
            if (currentFrame.Length == 0 || DateTime.UtcNow - feed.LastPlateDetectionUtc < AutoDetectionInterval) return;
            // Un QR aquí no suspende la lectura de placa en la otra cámara.
            if (DateTime.UtcNow - feed.LastQrVisibleUtc < TimeSpan.FromSeconds(2)) return;
            if (Interlocked.CompareExchange(ref feed.PlateDetectionInProgress, 1, 0) != 0) return;

            feed.LastPlateDetectionUtc = DateTime.UtcNow;
            _ = ProcessAutomaticDetectionAsync(feed, currentFrame);
        }

        /// <summary>Reconoce una placa estable y la muestra para que el operador registre la entrada.</summary>
        /// <param name="feed">Panel responsable del fotograma y de la región dibujada.</param>
        /// <param name="frame">Fotograma sobre el que se ejecuta la detección y el OCR.</param>
        /// <returns>Tarea que actualiza el estado visible tras la lectura.</returns>
        private async Task ProcessAutomaticDetectionAsync(CameraFeedViewModel feed, byte[] frame)
        {
            try
            {
                // El detector nativo se comparte y se usa de forma secuencial.
                await _plateReaderGate.WaitAsync();
                PlateDetectionResult detection;
                try { detection = await ((PlateReaderService)_plateService).DetectPlateWithRegionsAsync(frame); }
                finally { _plateReaderGate.Release(); }
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
                    if (detection.PlateNumber != feed.PendingPlate || DateTime.UtcNow - feed.PendingPlateUtc > TimeSpan.FromSeconds(12))
                    {
                        feed.PendingPlate = detection.PlateNumber;
                        feed.PendingPlateUtc = DateTime.UtcNow;
                        return;
                    }
                    // Se conserva la imagen global para una entrada manual y la región para este panel.
                    _currentDetection = detection;
                    _lastDetectionTime = DateTime.UtcNow;
                    feed.CurrentDetection = detection;
                    feed.LastDetectionUtc = _lastDetectionTime;
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
            finally { Interlocked.Exchange(ref feed.PlateDetectionInProgress, 0); }
        }

        /// <summary>Marca una placa como atendida para evitar repetir el evento mientras siga visible.</summary>
        /// <param name="plate">Placa recién registrada o mostrada.</param>
        private void MarkPlateHandled(string plate)
        {
            // La próxima lectura igual actualiza esta marca hasta que la placa desaparezca.
            _handledPlate = plate;
            _handledPlateLastSeenUtc = DateTime.UtcNow;
        }

        /// <summary>Determina si la placa ya atendida continúa ante la cámara.</summary>
        /// <param name="plate">Placa leída por el OCR.</param>
        /// <returns>Verdadero si aún no ha transcurrido el tiempo para considerarla una nueva aparición.</returns>
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

        /// <summary>Dibuja la zona de búsqueda y una sola caja para la placa reconocida.</summary>
        /// <param name="frameBytes">Fotograma original codificado.</param>
        /// <param name="detection">Resultado de OCR y región de placa, si existe.</param>
        /// <returns>Fotograma JPEG con las guías para el visor.</returns>
        private byte[] DrawDetectionOnFrame(byte[] frameBytes, PlateDetectionResult? detection)
        {
            using var mat = Cv2.ImDecode(frameBytes, ImreadModes.Color);
            // La zona cian indica dónde debe colocarse el vehículo para la lectura.
            var roi = PlateReaderService.GetRecognitionRegion(mat.Width, mat.Height);
            Cv2.Rectangle(mat, roi, Scalar.Cyan, 2, LineTypes.AntiAlias);
            if (detection?.HasDetection == true)
            {
                // El servicio ya escogió la región válida; no se dibujan las demás candidatas.
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
