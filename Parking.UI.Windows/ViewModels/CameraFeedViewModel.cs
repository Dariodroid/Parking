using Parking.Application.EntityService;
using Parking.Infrastructure.ExternalServices;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace Parking.UI.Windows.ViewModels;

/// <summary>Estado de un visor independiente: fuente, imagen y temporizadores de lectura.</summary>
public sealed class CameraFeedViewModel : BaseViewModel
{
    private CameraSource? _selectedSource;
    private string _genericNetworkUrl = string.Empty;
    private string _droidCamNetworkUrl = string.Empty;
    private string _status = "Seleccione una cámara.";
    private BitmapSource? _preview;
    private bool _isRunning;

    /// <summary>Identifica si este panel observa la entrada o la zona de salida.</summary>
    public string Title { get; }

    /// <summary>Fuentes locales disponibles y opción de flujo de red.</summary>
    public ObservableCollection<CameraSource> Sources { get; }

    /// <summary>Fuente elegida; la URL se escribe aparte cuando es una cámara de red.</summary>
    public CameraSource? SelectedSource
    {
        get => _selectedSource;
        set
        {
            // El cambio de fuente actualiza también la visibilidad de la URL.
            if (!SetProperty(ref _selectedSource, value)) return;
            OnPropertyChanged(nameof(IsNetworkSelected));
            OnPropertyChanged(nameof(IsDroidCamSelected));
            OnPropertyChanged(nameof(IsGenericNetworkSelected));
            // Al alternar perfiles se presenta la dirección guardada para cada uno.
            OnPropertyChanged(nameof(NetworkUrl));
            // El vídeo abierto conserva la fuente anterior hasta pulsar Iniciar.
            if (IsRunning) Status = "Selección modificada: pulse Iniciar para aplicar.";
        }
    }

    /// <summary>Dirección de vídeo del perfil de red seleccionado.</summary>
    public string NetworkUrl
    {
        get => IsDroidCamSelected ? _droidCamNetworkUrl : _genericNetworkUrl;
        set
        {
            // El campo visible modifica únicamente la dirección de este perfil.
            bool changed = IsDroidCamSelected
                ? SetProperty(ref _droidCamNetworkUrl, value)
                : SetProperty(ref _genericNetworkUrl, value);
            // La URL se aplica al volver a abrir la fuente, no durante una lectura.
            if (changed && IsRunning && IsNetworkSelected)
                Status = "URL modificada: pulse Iniciar para aplicar.";
        }
    }

    /// <summary>URL de la cámara IP genérica, aunque se muestre otro perfil.</summary>
    public string GenericNetworkUrl
    {
        get => _genericNetworkUrl;
        set
        {
            if (SetProperty(ref _genericNetworkUrl, value) && !IsDroidCamSelected)
                OnPropertyChanged(nameof(NetworkUrl));
        }
    }

    /// <summary>IP o URL del perfil DroidCam, aunque se muestre otro perfil.</summary>
    public string DroidCamNetworkUrl
    {
        get => _droidCamNetworkUrl;
        set
        {
            if (SetProperty(ref _droidCamNetworkUrl, value) && IsDroidCamSelected)
                OnPropertyChanged(nameof(NetworkUrl));
        }
    }

    /// <summary>Determina si se muestra la entrada de URL para esta fuente.</summary>
    public bool IsNetworkSelected => SelectedSource?.Kind == CameraSourceKind.NetworkStream;

    /// <summary>Indica que se eligió el acceso abreviado opcional de DroidCam.</summary>
    public bool IsDroidCamSelected => SelectedSource?.AddressProfile == CameraAddressProfile.DroidCam;

    /// <summary>Indica que se debe introducir una URL completa de cualquier cámara IP.</summary>
    public bool IsGenericNetworkSelected => IsNetworkSelected && !IsDroidCamSelected;

    /// <summary>Describe la conexión o el último error sin incluir credenciales de la URL.</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>Último fotograma visible para este panel.</summary>
    public BitmapSource? Preview
    {
        get => _preview;
        set => SetProperty(ref _preview, value);
    }

    /// <summary>Indica que este panel tiene una fuente abierta.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    // Cada panel posee su captura, por lo que un driver no bloquea al otro.
    internal ICameraService Capture { get; }

    // La fuente abierta puede diferir de la selección mientras el operador prepara un cambio.
    internal CameraSource? ActiveSource { get; set; }

    // La cancelación y la tarea permiten detener el bucle antes de liberar el driver.
    internal CancellationTokenSource? Cancellation { get; set; }
    internal Task? PreviewTask { get; set; }

    // Los límites de frecuencia son propios del panel; un QR en Salida no pausa el OCR de Entrada.
    internal int QrDetectionInProgress;
    internal DateTime LastQrDetectionUtc = DateTime.MinValue;
    internal DateTime LastQrVisibleUtc = DateTime.MinValue;
    internal int PlateDetectionInProgress;
    internal DateTime LastPlateDetectionUtc = DateTime.MinValue;

    // La imagen de la placa se conserva en el panel que la capturó.
    internal PlateDetectionResult? CurrentDetection;
    internal DateTime LastDetectionUtc = DateTime.MinValue;
    internal string PendingPlate = string.Empty;
    internal DateTime PendingPlateUtc = DateTime.MinValue;

    /// <summary>Crea un panel con su fuente de captura y una lista compartida de opciones.</summary>
    /// <param name="title">Etiqueta de la zona que observa el panel.</param>
    /// <param name="sources">Opciones que se actualizarán al buscar dispositivos.</param>
    /// <param name="cameraFactory">Crea una captura distinta para este panel.</param>
    public CameraFeedViewModel(string title, ObservableCollection<CameraSource> sources,
        ICameraServiceFactory cameraFactory)
    {
        // El título y el catálogo se exponen al XAML de cada panel.
        Title = title;
        Sources = sources;
        // La fábrica evita compartir un VideoCapture entre ambas imágenes.
        Capture = cameraFactory.Create();
    }
}
