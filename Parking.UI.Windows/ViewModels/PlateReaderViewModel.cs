using Parking.UI.Windows.Interfaces;
using Parking.Application.Interfaces;
using Parking.Application.Contracts;
using Parking.Application.Services;
using Parking.Application.UseCases;
using Parking.Domain.Model.Policies;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.Helpers;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels
{
    public class PlateReaderViewModel : BaseViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IVehicleTypeManagementService _vehicleTypes;
        private readonly IParkingSlotManagementService _slots;
        private static readonly TimeSpan AutoDetectionInterval = TimeSpan.FromMilliseconds(900);

        private readonly ICameraSourceCatalog _cameraSourceCatalog;
        private readonly CameraSelectionStore _cameraSelectionStore;
        private readonly CameraHealthMonitor _cameraHealth;
        private readonly CameraSelectionConfiguration? _savedCameraSelection;
        private readonly IPlateService _plateService;
        private readonly CameraPreviewService _cameraPreview;
        private readonly IEntryService _entryService;
        private readonly IExitService _exitService;
        private readonly IParkingStatusNotifier _parkingStatusNotifier;
        private readonly EntryTicketPrintService _ticketPrint;

        /// <summary>Habilita reimpresión después de una entrada ocasional confirmada.</summary>
        public bool CanReprintLastTicket => _ticketPrint.CanReprint;

        // Detector compartido: una lectura OCR a la vez protege el modelo nativo.
        private readonly SemaphoreSlim _plateReaderGate = new(1, 1);
        // Apertura y cierre se serializan para que una captura no se libere durante su inicio.
        private readonly SemaphoreSlim _cameraOperationGate = new(1, 1);
        private bool _isOperational = true;

        private string _plateNumber = string.Empty;
        private string _statusMessage = "Listo para iniciar.";
        private decimal _amountToCharge = 0;
        // Efectivo es el medio habitual; el operador puede cambiarlo para la próxima salida.
        private string _selectedPaymentMethod = "cash";
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
        private readonly PlateScanHistory _scanHistory = new();

        private readonly IQrService _qrService;
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
        /// <summary>Medio de pago aplicado a la próxima salida con importe.</summary>
        public string SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set => SetProperty(ref _selectedPaymentMethod, value);
        }
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
        /// <summary>Selecciona el medio que se guardará con el próximo cobro.</summary>
        public ICommand SelectPaymentMethodCommand { get; }
        public ICommand UseAutomaticSlotCommand { get; }
        /// <summary>Envía de nuevo el último ticket sin crear otra sesión.</summary>
        public ICommand ReprintLastTicketCommand { get; }

        /// <summary>Conecta cámara, reconocimiento, sesiones y catálogos usados en la operación de entrada y salida.</summary>
        /// <param name="cameraFactory">Crea una captura distinta para cada visor.</param>
        /// <param name="cameraSourceCatalog">Busca cámaras conectadas a Windows.</param>
        /// <param name="cameraSelectionStore">Recupera y protege las selecciones de fuentes.</param>
        /// <param name="cameraHealth">Registra fallos activos de vídeo para el centro de control.</param>
        /// <param name="plateService">Reconoce placas dentro del fotograma.</param>
        /// <param name="qrService">Lee los tickets QR mostrados a la cámara.</param>
        /// <param name="entryService">Registra y consulta las sesiones de estacionamiento.</param>
        /// <param name="exitService">Consulta y cierra las sesiones de estacionamiento.</param>
        /// <param name="vehicleTypes">Proporciona tipos de vehículo disponibles.</param>
        /// <param name="slots">Consulta la ocupación y los puestos libres.</param>
        /// <param name="parkingStatusNotifier">Notifica cambios de ocupación a otras vistas.</param>
        /// <param name="dialogService">Presenta avisos y errores al operador.</param>
        /// <param name="ticketPrint">Conserva e imprime el último ticket de entrada.</param>
        /// <param name="cameraPreview">Muestra las imágenes y vigila que cada cámara siga entregándolas.</param>
        public PlateReaderViewModel(
            ICameraServiceFactory cameraFactory,
            ICameraSourceCatalog cameraSourceCatalog,
            CameraSelectionStore cameraSelectionStore,
            CameraHealthMonitor cameraHealth,
            IPlateService plateService,
            IQrService qrService,
            IEntryService entryService,
            IExitService exitService,
            IVehicleTypeManagementService vehicleTypes,
            IParkingSlotManagementService slots,
            IParkingStatusNotifier parkingStatusNotifier,
            IDialogService dialogService,
            EntryTicketPrintService ticketPrint,
            CameraPreviewService cameraPreview)
        {
            _dialogService = dialogService;
            _ticketPrint = ticketPrint;
            _cameraSourceCatalog = cameraSourceCatalog;
            _cameraSelectionStore = cameraSelectionStore;
            _cameraHealth = cameraHealth;
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
            _cameraPreview = cameraPreview;
            _entryService = entryService;
            _exitService = exitService;
            _qrService = qrService;
            _vehicleTypes = vehicleTypes;
            _slots = slots;
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
            ReprintLastTicketCommand = new RelayCommand(_ => ReprintLastTicket());
            SelectVehicleTypeCommand = new RelayCommand(param =>
            {
                if (param is vehicle_type selectedType) SelectedVehicleType = selectedType;
            });
            SelectPaymentMethodCommand = new RelayCommand(param =>
            {
                // Las opciones coinciden con los valores admitidos por payments en SQL.
                if (param is string method && method is "cash" or "transfer" or "card" or "other")
                    SelectedPaymentMethod = method;
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

        /// <summary>Actualiza total, ocupados y puestos disponibles conservando la selección actual.</summary>
        private async Task LoadSlotStatsAsync()
        {
            var slots = (await _slots.GetAllAsync()).ToList();
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

        /// <summary>Carga solo tipos activos para registrar entradas desde Operaciones.</summary>
        private async Task LoadVehicleTypesAsync()
        {
            VehicleTypes.Clear();
            var items = await _vehicleTypes.GetAllAsync();
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
            if (_scanHistory.WasRecentlyExited(plate, DateTime.UtcNow))
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
                        _scanHistory.MarkPlateHandled(plate, DateTime.UtcNow);
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
                    ShowRegisteredEntry(plate, entry);
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

        /// <summary>Conserva el ticket y comunica la modalidad con el diálogo adecuado.</summary>
        private void ShowRegisteredEntry(string plate, EntryRegistrationResult entry)
        {
            string? printingWarning = _ticketPrint.PrintIfConfigured(entry.Ticket);
            if (entry.Ticket is not null) OnPropertyChanged(nameof(CanReprintLastTicket));
            string accessMessage = DescribeEntryAccess(entry.Access);
            if (printingWarning is not null) accessMessage += $" {printingWarning}";
            StatusMessage = $"ENTRADA: {plate} asignado al puesto {entry.SlotNumber}. {accessMessage}";
            ScannerAudioFeedback.PlayEntry();
            if (entry.Access.PendingFee > 0 || printingWarning is not null)
                _dialogService.ShowWarning(printingWarning is not null
                    ? "Entrada guardada: revise la impresión"
                    : "Entrada registrada: cuota pendiente", StatusMessage);
            else
                _dialogService.ShowSuccess("Entrada registrada", StatusMessage);
            _parkingStatusNotifier.NotifyParkingStatusChanged();
        }

        /// <summary>Traduce la clasificación de Domain y agrega la cuota pendiente si existe.</summary>
        private static string DescribeEntryAccess(MonthlyAccessDecision access)
        {
            string message = access.Kind switch
            {
                MonthlyAccessKind.Monthly => "Cliente mensualizado: entrada registrada sin ticket y sin cobro de estancia.",
                MonthlyAccessKind.OutsideSchedule => "Fuera del horario mensual. Se aplicará la tarifa ocasional.",
                MonthlyAccessKind.Expired => "Contrato vencido. Se aplicará la tarifa ocasional.",
                MonthlyAccessKind.Inactive => "Contrato inactivo. Se aplicará la tarifa ocasional.",
                MonthlyAccessKind.NotStarted => "El contrato aún no inicia. Se aplicará la tarifa ocasional.",
                _ => "Se aplicará la tarifa ocasional."
            };
            if (access.PendingFee > 0)
                message += $" Cuota mensual pendiente: {CurrencyDisplay.Format(access.PendingFee)}.";
            return message;
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

        /// <summary>Reimprime el último ticket ocasional sin volver a registrar el vehículo.</summary>
        private void ReprintLastTicket()
        {
            if (!_ticketPrint.CanReprint) return;
            try
            {
                // Se leen ajustes actuales para permitir cambiar de impresora tras un fallo.
                _ticketPrint.ReprintLast();
                _dialogService.ShowSuccess("Ticket enviado", "El ticket se envió a la cola de impresión de Windows.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning("Ticket pendiente", $"No se pudo enviar el ticket: {ex.Message}");
            }
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
                    _cameraHealth.ReportFailure(feed.Title, feed.Status);
                    return;
                }

                // El bucle conserva su propia cancelación y sus propias estadísticas OCR/QR.
                feed.Cancellation = new CancellationTokenSource();
                feed.ActiveSource = source;
                feed.IsRunning = true;
                _cameraHealth.Clear(feed.Title);
                feed.Status = "Imagen en directo. Lee placas y QR.";
                feed.PreviewTask = _cameraPreview.RunAsync(feed, (source, frame) =>
                {
                    TryQueueQrDetection(source, frame);
                    TryQueuePlateDetection(source, frame);
                }, () => OnPropertyChanged(nameof(IsCameraRunning)), feed.Cancellation.Token);
            }
            catch (Exception)
            {
                // No se presenta la excepción: una URL podría incluir credenciales.
                await feed.Capture.StopCameraAsync();
                feed.ActiveSource = null;
                feed.IsRunning = false;
                feed.Status = "No se pudo conectar esta cámara. Revise la fuente.";
                _cameraHealth.ReportFailure(feed.Title, feed.Status);
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
        private async Task StopFeedAsync(CameraFeedViewModel feed)
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
            if (released) _cameraHealth.Clear(feed.Title);
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
                    if (_scanHistory.ShouldIgnoreRepeatedQr(qrText, now))
                    {
                        // Mientras el ticket siga ante la cámara, una sola lectura
                        // basta. La siguiente se permite después de retirarlo.
                        return;
                    }

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
                        ? await _exitService.GetActiveSessionByQrAsync(identifier)
                        : await _exitService.GetActiveSessionByPlateAsync(identifier);

                    success = session != null && (isQr
                        ? await _exitService.RegisterExitByQrAsync(identifier, SelectedPaymentMethod)
                        : await _exitService.RegisterExitByPlateAsync(identifier, SelectedPaymentMethod));

                    if (success)
                    {
                        DateTime exitRecordedUtc = DateTime.UtcNow;
                        _scanHistory.MarkPlateHandled(session!.plate, exitRecordedUtc);
                        _scanHistory.MarkExited(session.plate, session.qr_data, exitRecordedUtc);
                        await LoadSlotStatsAsync();
                    }
                }
                finally { _sessionOperationGate.Release(); }

                if (session == null)
                {
                    await ShowMissingSessionAsync(identifier, isQr, showMissingSession);
                    return false;
                }

                if (!success)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        _dialogService.ShowError("Error", $"No se pudo registrar la salida de {session.plate}."));
                    return false;
                }

                await ShowRegisteredExitAsync(session, isQr);
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

        /// <summary>Informa que la placa o el QR no corresponde a una estancia abierta.</summary>
        private async Task ShowMissingSessionAsync(string identifier, bool isQr, bool showDialog)
        {
            if (showDialog)
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AmountToCharge = 0;
                    _dialogService.ShowWarning("Atención", $"No hay sesión activa para {identifier}.");
                });
            else if (isQr)
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    StatusMessage = "QR inválido o sesión ya cerrada.");
        }

        /// <summary>Muestra el cobro confirmado y limpia las lecturas anteriores de ambos visores.</summary>
        private async Task ShowRegisteredExitAsync(parking_session session, bool isQr)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AmountToCharge = session.amount_due ?? 0;
                SelectedPaymentMethod = "cash";
                PlateNumber = string.Empty;
                _currentDetection = null;
                EntranceFeed.PendingPlate = string.Empty;
                ExitFeed.PendingPlate = string.Empty;
                bool isMonthly = session.notes == MonthlyAccessPolicy.MonthlySessionNote;
                StatusMessage = isMonthly
                    ? $"SALIDA POR PLACA: {session.plate} | Cliente mensualizado, sin cobro."
                    : $"SALIDA POR {(isQr ? "QR" : "PLACA")}: {session.plate} | Total: {CurrencyDisplay.Format(AmountToCharge)}";
                ScannerAudioFeedback.PlayExit();
                _dialogService.ShowSuccess("¡Éxito!", StatusMessage);
            });
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
                try { detection = await _plateService.DetectPlateWithRegionsAsync(frame); }
                finally { _plateReaderGate.Release(); }
                if (detection.HasPlateCandidates && string.IsNullOrWhiteSpace(detection.PlateNumber))
                {
                    // El OCR puede fallar unos frames aunque el vehículo continúe
                    // frente a la cámara; no lo tratamos como una nueva llegada.
                    _scanHistory.KeepHandledPlateVisible(DateTime.UtcNow);
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
                    if (_scanHistory.ShouldIgnoreRepeatedPlate(detection.PlateNumber, DateTime.UtcNow)) return;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        PlateNumber = detection.PlateNumber;
                        StatusMessage = $"Placa reconocida: {detection.PlateNumber}. Registre la entrada o escanee el QR para la salida.";
                    });

                    // En la misma puerta no se puede inferir la dirección del
                    // vehículo a partir de la placa. La cámara solo rellena el
                    // campo; la salida automática requiere el QR del ticket.
                    _scanHistory.MarkPlateHandled(detection.PlateNumber, DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    StatusMessage = $"Error al reconocer la placa: {ex.Message}");
            }
            finally { Interlocked.Exchange(ref feed.PlateDetectionInProgress, 0); }
        }

    }
}
