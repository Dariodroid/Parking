using Parking.UI.Windows.Interfaces;
using Parking.Application.Services;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

/// <summary>Presenta incidencias, revisiones y la conciliación del turno del usuario actual.</summary>
public sealed class OperationsControlViewModel : BaseViewModel
{
    private readonly IControlIncidentService _incidents;
    private readonly IShiftClosingService _shifts;
    private readonly CameraHealthMonitor _cameras;
    private readonly IDialogService _dialogs;
    private ControlIncident? _selectedIncident;
    private ShiftSummary _currentShift = new(DateTime.Today, DateTime.Now, 0, 0, 0, 0, 0);
    private string _reviewReason = string.Empty;
    private string _closingNote = string.Empty;
    private string _countedCash = string.Empty;
    private string _status = string.Empty;

    public ObservableCollection<ControlIncident> Incidents { get; } = new();
    public ObservableCollection<IncidentReview> Reviews { get; } = new();
    public ObservableCollection<ShiftClosure> Closures { get; } = new();
    public ControlIncident? SelectedIncident
    {
        get => _selectedIncident;
        set
        {
            // La foto se comprueba en la interfaz local, sin añadir acceso a archivos al DTO.
            if (SetProperty(ref _selectedIncident, value)) OnPropertyChanged(nameof(SelectedIncidentHasPhoto));
        }
    }
    /// <summary>Indica si la foto de la incidencia existe en este equipo.</summary>
    public bool SelectedIncidentHasPhoto => !string.IsNullOrWhiteSpace(SelectedIncident?.PhotoPath)
        && System.IO.File.Exists(SelectedIncident.PhotoPath);
    public ShiftSummary CurrentShift { get => _currentShift; private set => SetProperty(ref _currentShift, value); }
    public string ReviewReason { get => _reviewReason; set => SetProperty(ref _reviewReason, value); }
    public string ClosingNote { get => _closingNote; set => SetProperty(ref _closingNote, value); }
    public string CountedCash { get => _countedCash; set => SetProperty(ref _countedCash, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public int IncidentCount => Incidents.Count;
    public int PaymentDifferenceCount => Incidents.Count(x => x.Category == "Diferencia de cobro");
    public int CameraFailureCount => Incidents.Count(x => x.Category == "Cámara");
    public bool IsAdministrator => string.Equals(CurrentUser.Role, "Administrador", StringComparison.OrdinalIgnoreCase);
    public ICommand RefreshCommand { get; }
    public ICommand ReviewCommand { get; }
    public ICommand CloseShiftCommand { get; }

    /// <summary>Prepara el centro sin consultar SQL hasta que el administrador navega a él.</summary>
    /// <param name="incidents">Consulta de incidencias y escritura de revisiones.</param>
    /// <param name="shifts">Cálculos y cierres de caja del turno.</param>
    /// <param name="cameras">Fallos de vídeo del proceso actual.</param>
    /// <param name="dialogs">Muestra los avisos y confirmaciones con CustomMessageBox.</param>
    public OperationsControlViewModel(IControlIncidentService incidents, IShiftClosingService shifts,
        CameraHealthMonitor cameras,
        IDialogService dialogs)
    {
        _incidents = incidents;
        _shifts = shifts;
        _cameras = cameras;
        _dialogs = dialogs;
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        ReviewCommand = new AsyncRelayCommand(_ => ReviewAsync());
        CloseShiftCommand = new AsyncRelayCommand(_ => CloseShiftAsync());
    }

    /// <summary>Actualiza alertas y cierres sin modificar los registros operativos.</summary>
    public async Task LoadAsync()
    {
        try
        {
            Status = "Actualizando el centro de control...";
            var incidents = IsAdministrator ? await _incidents.GetOpenIncidentsAsync() : [];
            var reviews = IsAdministrator ? await _incidents.GetReviewsAsync() : [];
            var closures = await _shifts.GetClosuresAsync(IsAdministrator ? null : CurrentUser.Id);
            var shift = await _shifts.GetCurrentShiftAsync(CurrentUser.Id);
            Incidents.Clear();
            foreach (var item in incidents.Concat(IsAdministrator ? _cameras.GetIncidents() : [])
                .OrderByDescending(x => x.OccurredAt))
                Incidents.Add(item);
            Reviews.Clear();
            foreach (var item in reviews) Reviews.Add(item);
            Closures.Clear();
            foreach (var item in closures) Closures.Add(item);
            CurrentShift = shift;
            OnPropertyChanged(nameof(IncidentCount));
            OnPropertyChanged(nameof(PaymentDifferenceCount));
            OnPropertyChanged(nameof(CameraFailureCount));
            Status = !IsAdministrator ? "Turno actualizado. Registre el efectivo contado al finalizar." :
                Incidents.Count == 0 ? "Sin incidencias pendientes en la revisión actual." :
                $"{Incidents.Count} incidencia(s) requieren atención.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Centro de control: {ex}");
            Status = "No se pudieron cargar los datos. Compruebe la conexión y los permisos SQL.";
            _dialogs.ShowError("Centro de control", Status);
        }
    }

    /// <summary>Guarda la explicación del administrador, sin borrar ni ajustar automáticamente pagos.</summary>
    private async Task ReviewAsync()
    {
        if (!IsAdministrator)
        {
            Status = "Solo un administrador puede atender incidencias.";
            _dialogs.ShowWarning("Centro de control", Status);
            return;
        }
        if (SelectedIncident is null)
        {
            Status = "Seleccione una incidencia.";
            _dialogs.ShowWarning("Centro de control", Status);
            return;
        }
        if (SelectedIncident.Category == "Cámara")
        {
            Status = "Reinicie la cámara desde Operaciones; la alerta desaparecerá al volver la imagen.";
            _dialogs.ShowInfo("Cámara sin imagen", Status);
            return;
        }
        if (SelectedIncident.Category == "Cuota vencida")
        {
            Status = "Registre el pago de la cuota desde Clientes; la alerta desaparecerá al actualizar el plan.";
            _dialogs.ShowInfo("Cuota vencida", Status);
            return;
        }
        try
        {
            await _incidents.ReviewAsync(SelectedIncident.Key, ReviewReason, CurrentUser.Id);
            ReviewReason = string.Empty;
            SelectedIncident = null;
            Status = "Revisión guardada con usuario, fecha y motivo.";
            _dialogs.ShowSuccess("Incidencia revisada", Status);
            await LoadAsync();
        }
        catch (ArgumentException ex)
        {
            Status = ex.Message;
            _dialogs.ShowWarning("Revise el motivo", Status);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Revisión de incidencia: {ex}");
            Status = "No se pudo guardar la revisión. Actualice la lista y vuelva a intentarlo.";
            _dialogs.ShowError("Centro de control", Status);
        }
    }

    /// <summary>Cierra el intervalo del operador autenticado con el efectivo contado.</summary>
    private async Task CloseShiftAsync()
    {
        // No se admiten separadores de miles: "1,005" nunca debe interpretarse como 1005.
        if (!decimal.TryParse(CountedCash, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CurrencyDisplay.Culture, out decimal counted))
        {
            Status = "Escriba el efectivo contado como un importe válido.";
            _dialogs.ShowWarning("Cierre de cobros", Status);
            return;
        }
        if (counted < 0 || decimal.Round(counted, 2) != counted)
        {
            Status = "El efectivo contado debe ser positivo y tener como máximo dos decimales.";
            _dialogs.ShowWarning("Cierre de cobros", Status);
            return;
        }
        // El cierre crea un asiento permanente; el operador confirma el importe antes de firmarlo.
        if (!_dialogs.ShowConfirmation("Confirmar cierre",
            $"Efectivo esperado: {CurrencyDisplay.Format(CurrentShift.Cash)}\nEfectivo contado: {CurrencyDisplay.Format(counted)}\n\n¿Guardar el cierre de cobros?"))
            return;
        try
        {
            ShiftClosure closure = await _shifts.CloseShiftAsync(CurrentUser.Id, counted, ClosingNote);
            CountedCash = string.Empty;
            ClosingNote = string.Empty;
            Status = $"Turno #{closure.Id} cerrado. Diferencia de efectivo: {CurrencyDisplay.Format(closure.Difference)}.";
            _dialogs.ShowSuccess("Cierre guardado", Status);
            await LoadAsync();
        }
        catch (ArgumentException ex)
        {
            Status = ex.Message;
            _dialogs.ShowWarning("Cierre de cobros", Status);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cierre de turno: {ex}");
            Status = "No se pudo cerrar el turno. Ningún cierre nuevo quedó confirmado.";
            _dialogs.ShowError("Cierre de cobros", Status);
        }
    }
}
