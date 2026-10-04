using Parking.UI.Windows.Interfaces;
using Parking.Application.Services;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Policies;
using Parking.Domain.Model.Interfaces;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.View.Dialogs;
using Parking.UI.Windows.Helpers;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class RegisteredVehicleViewModel : BaseViewModel
{
    private readonly IDialogService _dialogService;
    private readonly IRegisteredVehicleManagementService _service;
    private readonly IMonthlyFeeLedgerService _monthlyLedger;


    private readonly int _currentUserId = CurrentUser.Id;

    public ObservableCollection<registered_vehicle> RegisteredVehicles { get; } = new();

    public ObservableCollection<vehicle_type> VehicleTypes { get; } = new();

    public ObservableCollection<VehicleScheduleItemViewModel> VehicleSchedules { get; } = new();

    public ICommand SaveCommand { get; }

    public ICommand UpdateCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand NewCommand { get; }
    /// <summary>Comando que registra como pagada la cuota vencida del cliente seleccionado.</summary>
    public ICommand MarkMonthlyFeePaidCommand { get; }

    /// <summary>Elige el medio real de cobro para la próxima cuota mensual.</summary>
    public ICommand SelectMonthlyPaymentMethodCommand { get; }

    private string _selectedMonthlyPaymentMethod = "cash";
    /// <summary>Medio seleccionado; efectivo se propone inicialmente.</summary>
    public string SelectedMonthlyPaymentMethod
    {
        get => _selectedMonthlyPaymentMethod;
        set => SetProperty(ref _selectedMonthlyPaymentMethod, value);
    }

    private decimal _pendingMonthlyFee;
    /// <summary>Cuota vencida que se muestra en la ficha; cero si no hay saldo pendiente.</summary>
    public decimal PendingMonthlyFee
    {
        get => _pendingMonthlyFee;
        private set => SetProperty(ref _pendingMonthlyFee, value);
    }

    /// <summary>Prepara los comandos y los siete días editables del cliente mensualizado.</summary>
    /// <param name="service">Consulta y guarda vehículo, plan y horarios.</param>
    /// <param name="dialogService">Muestra confirmaciones, avisos y errores al operador.</param>
    /// <param name="monthlyLedger">Confirma el pago y guarda su asiento sin alterar la vigencia.</param>
    public RegisteredVehicleViewModel(IRegisteredVehicleManagementService service, IDialogService dialogService,
        IMonthlyFeeLedgerService monthlyLedger)
    {
        _dialogService = dialogService;
        _service = service;
        _monthlyLedger = monthlyLedger;


        SaveCommand = new RelayCommand(async _ => await SaveAsync());

        UpdateCommand = new RelayCommand(async _ => await UpdateAsync());

        DeleteCommand = new RelayCommand(async _ => await DeleteAsync());

        NewCommand = new RelayCommand(_ => ClearForm());
        // La acción de pago espera el guardado antes de poder ejecutarse otra vez.
        MarkMonthlyFeePaidCommand = new AsyncRelayCommand(_ => MarkMonthlyFeePaidAsync());
        SelectMonthlyPaymentMethodCommand = new RelayCommand(value =>
        {
            if (value is string method && method is "cash" or "transfer" or "card")
                SelectedMonthlyPaymentMethod = method;
        });

        InitializeSchedules();
    }

    #region PROPERTIES

    private int _id;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _plate = string.Empty;

    public string Plate
    {
        get => _plate;
        set
        {
            var formattedPlate = PlateFormatter.Format(value);

            if (!SetProperty(ref _plate, formattedPlate)
                && !string.Equals(value, formattedPlate, StringComparison.Ordinal))
            {
                // Restores the displayed value when an invalid or excess character is typed.
                OnPropertyChanged();
            }
        }
    }

    private int _vehicleTypeId;

    public int VehicleTypeId
    {
        get => _vehicleTypeId;
        set => SetProperty(ref _vehicleTypeId, value);
    }

    private string _ownerName = string.Empty;

    public string OwnerName
    {
        get => _ownerName;
        set => SetProperty(ref _ownerName, value);
    }

    private string _ownerPhone = string.Empty;

    public string OwnerPhone
    {
        get => _ownerPhone;
        set => SetProperty(ref _ownerPhone, value);
    }

    private string _ownerEmail = string.Empty;

    public string OwnerEmail
    {
        get => _ownerEmail;
        set => SetProperty(ref _ownerEmail, value);
    }

    private string _ownerCedula = string.Empty;

    public string OwnerCedula
    {
        get => _ownerCedula;
        set => SetProperty(ref _ownerCedula, value);
    }

    private decimal? _monthlyFee;

    public decimal? MonthlyFee
    {
        get => _monthlyFee;
        set
        {
            if (value < 0)
                value = 0;

            SetProperty(ref _monthlyFee, value);
        }
    }

    private DateTime? _monthlyStartDate = DateTime.Today;

    public DateTime? MonthlyStartDate
    {
        get => _monthlyStartDate;
        set => SetProperty(ref _monthlyStartDate, value);
    }

    private DateTime? _monthlyEndDate = DateTime.Today.AddMonths(1);

    public DateTime? MonthlyEndDate
    {
        get => _monthlyEndDate;
        set => SetProperty(ref _monthlyEndDate, value);
    }

    private string _notes = string.Empty;

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    private bool _isActive = true;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private string _statusMessage = string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private registered_vehicle? _selectedRegisteredVehicle;

    public registered_vehicle? SelectedRegisteredVehicle
    {
        get => _selectedRegisteredVehicle;
        set
        {
            if (SetProperty(ref _selectedRegisteredVehicle, value)
                && value != null)
            {
                LoadSelected(value);
            }
        }
    }

    #endregion

    private bool _isLoaded;

    /// <summary>Prepara los siete días editables con su estado y horas iniciales.</summary>
    private void InitializeSchedules()
    {
        VehicleSchedules.Clear();
        string[] dayNames =
        {
            "LUNES", "MARTES", "MIÉRCOLES", "JUEVES",
            "VIERNES", "SÁBADO", "DOMINGO"
        };

        // La pantalla empieza en lunes, aunque la base de datos representa domingo con 0.
        for (int index = 0; index < dayNames.Length; index++)
        {
            bool isSunday = index == 6;
            bool isWeekend = index >= 5;
            int dayOfWeek = isSunday ? 0 : index + 1;
            VehicleSchedules.Add(new VehicleScheduleItemViewModel
            {
                DayOfWeek = dayOfWeek,
                DayName = dayNames[index],
                IsEnabled = !isSunday,
                StartTime = TimeSpan.FromHours(isWeekend ? 8 : 7),
                EndTime = TimeSpan.FromHours(isWeekend ? 18 : 19),
                IsFullDay = false
            });
        }
    }

    /// <summary>Carga una sola vez los tipos, clientes y horarios necesarios para el formulario.</summary>
    public async Task InitializeAsync()
    {
        if (_isLoaded)
            return;

        _isLoaded = true;

        await LoadVehicleTypesAsync();

        InitializeSchedules();

        await LoadAsync();
    }

    /// <summary>Consulta los tipos de vehículo disponibles y omite los eliminados.</summary>
    private async Task LoadVehicleTypesAsync()
    {
        VehicleTypes.Clear();

        var items = await _service.GetVehicleTypesAsync();

        foreach (var item in items)
        {
            if (!item.is_deleted)
            {
                VehicleTypes.Add(item);
            }
        }
    }

    /// <summary>Recarga los clientes con planes y horarios desde la base de datos.</summary>
    private async Task LoadAsync()
    {
        RegisteredVehicles.Clear();

        var items = await _service.GetAllCompleteAsync();

        foreach (var item in items)
        {
            RegisteredVehicles.Add(item);
        }
    }

    /// <summary>Traslada a la ficha los datos, la deuda y los horarios del cliente elegido.</summary>
    /// <param name="item">Vehículo seleccionado con plan y horarios cargados.</param>
    private void LoadSelected(registered_vehicle item)
    {
        // Se recalcula la cuota con la fecha actual al seleccionar la fila.
        PendingMonthlyFee = MonthlyAccessPolicy.Evaluate(item, DateTime.Now).PendingFee;
        Id = item.id;
        Plate = item.plate;
        VehicleTypeId = item.vehicle_type_id;
        OwnerName = item.owner_name ?? string.Empty;
        OwnerPhone = item.owner_phone ?? string.Empty;
        OwnerEmail = item.owner_email ?? string.Empty;
        OwnerCedula = item.owner_cedula ?? string.Empty;
        Notes = item.notes ?? string.Empty;
        IsActive = item.is_active;
        if (item.vehicle_monthly_plan is { } plan)
        {
            MonthlyFee = plan.monthly_fee;
            MonthlyStartDate = plan.start_date;
            MonthlyEndDate = plan.end_date;
        }
        LoadSchedules(item);
    }

    /// <summary>Restablece los siete días y carga solo los horarios no eliminados de la ficha.</summary>
    private void LoadSchedules(registered_vehicle item)
    {
        foreach (var schedule in VehicleSchedules)
        {
            schedule.IsEnabled = false;
            schedule.IsFullDay = false;
            schedule.StartTime = new TimeSpan(7, 0, 0);
            schedule.EndTime = new TimeSpan(19, 0, 0);
        }
        if (item.monthly_vehicle_schedules == null || !item.monthly_vehicle_schedules.Any())
            return;
        foreach (var schedule in VehicleSchedules)
        {
            var saved = item.monthly_vehicle_schedules
                .FirstOrDefault(x => !x.is_deleted && x.day_of_week == schedule.DayOfWeek);
            if (saved == null) continue;
            // Una fila guardada pero inactiva debe verse desactivada.
            schedule.IsEnabled = saved.is_active;
            schedule.StartTime = saved.start_time.ToTimeSpan();
            schedule.EndTime = saved.end_time.ToTimeSpan();
            schedule.IsFullDay = saved.is_full_day;
        }
    }

    /// <summary>Comprueba la ficha y las fechas antes de crear o actualizar un contrato.</summary>
    /// <returns>Verdadero cuando los datos son aptos para guardarse sin redondear importes.</returns>
    private bool Validate()
    {
        string? error = RegisteredVehicleFormValidator.GetError(
            Plate, VehicleTypeId, MonthlyFee, MonthlyStartDate, MonthlyEndDate);
        if (error is null) return true;
        _dialogService.ShowWarning("Atención", error);
        return false;
    }

    /// <summary>Construye la ficha que acompaña al nuevo contrato mensual.</summary>
    private registered_vehicle CreateVehicle() => new()
    {
        plate = Plate.Trim().ToUpper(),
        vehicle_type_id = VehicleTypeId,
        owner_name = OwnerName?.Trim(),
        owner_phone = OwnerPhone?.Trim(),
        owner_email = OwnerEmail?.Trim(),
        owner_cedula = OwnerCedula?.Trim(),
        notes = Notes?.Trim(),
        is_active = IsActive,
        created_at = DateTime.Now,
        created_by = _currentUserId,
        is_deleted = false
    };

    /// <summary>Conserva como pendiente la cuota de un plan que se crea ya vencido.</summary>
    private vehicle_monthly_plan CreatePlan(DateTime startDate, DateTime endDate, bool expired) => new()
    {
        registered_vehicle_id = 0,
        monthly_fee = MonthlyFee ?? 0,
        start_date = startDate,
        end_date = endDate,
        payment_date = expired ? startDate : DateTime.Now,
        is_active = IsActive,
        notes = Notes?.Trim(),
        created_at = DateTime.Now,
        created_by = _currentUserId,
        is_deleted = false,
        status = IsActive ? "active" : "cancelled",
        collected_by = expired ? null : _currentUserId
    };

    /// <summary>Convierte un día habilitado de la pantalla en una fila nueva del horario.</summary>
    private monthly_vehicle_schedule CreateSchedule(VehicleScheduleItemViewModel day, int vehicleId) => new()
    {
        registered_vehicle_id = vehicleId,
        day_of_week = day.DayOfWeek,
        start_time = TimeOnly.FromTimeSpan(day.StartTime),
        end_time = TimeOnly.FromTimeSpan(day.EndTime),
        is_active = day.IsEnabled,
        is_full_day = day.IsFullDay,
        created_at = DateTime.Now,
        created_by = _currentUserId,
        is_deleted = false
    };

    /// <summary>Crea vehículo, plan y horarios; deja pendiente la cuota de un contrato creado ya vencido.</summary>
    private async Task SaveAsync()
    {
        try
        {
            if (!Validate())
                return;

            var exists =
                await _service.ExistsByPlateAsync(
                    Plate.Trim().ToUpper());

            if (exists)
            {
                _dialogService.ShowWarning("Atención", "La placa ya existe.");
                return;
            }

            var vehicle = CreateVehicle();

            // Un contrato histórico que se crea ya vencido debe conservar una cuota pendiente.
            // Usamos su inicio como última fecha conocida del período anterior; no declaramos un cobro nuevo.
            DateTime startDate = MonthlyStartDate!.Value;
            DateTime endDate = MonthlyEndDate!.Value;
            bool createdAlreadyExpired = endDate.Date < DateTime.Today;

            var plan = CreatePlan(startDate, endDate, createdAlreadyExpired);
            var schedules = VehicleSchedules.Where(x => x.IsEnabled)
                .Select(x => CreateSchedule(x, 0)).ToList();

            await _service.RegisterAsync(vehicle, plan, schedules);

            StatusMessage = createdAlreadyExpired
                ? "Cliente registrado con contrato vencido. Seleccione su ficha para registrar la cuota pendiente."
                : "Cliente mensualizado registrado.";
            if (createdAlreadyExpired)
                _dialogService.ShowWarning("Clientes", StatusMessage);
            else
                _dialogService.ShowSuccess("Clientes", StatusMessage);

            await LoadAsync();

            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _dialogService.ShowError("Clientes", StatusMessage);
        }
    }

    /// <summary>Aplica a la entidad los campos editados de la ficha.</summary>
    private void ApplyVehicleChanges(registered_vehicle entity)
    {
        entity.plate = Plate.Trim().ToUpper();
        entity.vehicle_type_id = VehicleTypeId;
        entity.owner_name = OwnerName?.Trim();
        entity.owner_phone = OwnerPhone?.Trim();
        entity.owner_email = OwnerEmail?.Trim();
        entity.owner_cedula = OwnerCedula?.Trim();
        entity.notes = Notes?.Trim();
        entity.is_active = IsActive;
        entity.updated_at = DateTime.Now;
        entity.updated_by = _currentUserId;
    }

    /// <summary>Actualiza las fechas y el estado del plan existente sin registrar un pago.</summary>
    private void ApplyPlanChanges(vehicle_monthly_plan? plan)
    {
        if (plan is null) return;
        plan.monthly_fee = MonthlyFee ?? 0;
        plan.start_date = MonthlyStartDate ?? DateTime.Today;
        plan.end_date = MonthlyEndDate ?? DateTime.Today.AddMonths(1);
        plan.is_active = IsActive;
        plan.notes = Notes?.Trim();
        plan.updated_at = DateTime.Now;
        plan.updated_by = _currentUserId;
        plan.status = IsActive ? "active" : "cancelled";
    }

    /// <summary>Separa los horarios que se actualizan de los nuevos días habilitados.</summary>
    private (List<monthly_vehicle_schedule> ToUpdate, List<monthly_vehicle_schedule> ToAdd)
        BuildScheduleChanges(registered_vehicle entity)
    {
        var toUpdate = new List<monthly_vehicle_schedule>();
        var toAdd = new List<monthly_vehicle_schedule>();
        foreach (var day in VehicleSchedules)
        {
            var existing = entity.monthly_vehicle_schedules
                .FirstOrDefault(x => x.day_of_week == day.DayOfWeek);
            if (existing is not null)
            {
                existing.start_time = TimeOnly.FromTimeSpan(day.StartTime);
                existing.end_time = TimeOnly.FromTimeSpan(day.EndTime);
                existing.is_full_day = day.IsFullDay;
                existing.is_active = day.IsEnabled;
                existing.updated_at = DateTime.Now;
                existing.updated_by = _currentUserId;
                toUpdate.Add(existing);
            }
            else if (day.IsEnabled)
                toAdd.Add(CreateSchedule(day, entity.id));
        }
        return (toUpdate, toAdd);
    }

    /// <summary>Actualiza la ficha, el estado, las fechas y los horarios sin registrar un cobro nuevo.</summary>
    private async Task UpdateAsync()
    {
        try
        {
            if (!Validate())
                return;

            if (Id == 0)
            {
                _dialogService.ShowWarning("Atención", "Seleccione un registro.");
                return;
            }

            var entity = await _service.GetByIdAsync(Id);

            if (entity == null)
            {

                _dialogService.ShowWarning("Atención", "Registro no encontrado.");
                return;
            }

            ApplyVehicleChanges(entity);
            ApplyPlanChanges(entity.vehicle_monthly_plan);
            var (schedulesToUpdate, schedulesToAdd) = BuildScheduleChanges(entity);

            await _service.UpdateAsync(entity, schedulesToUpdate, schedulesToAdd);

            _dialogService.ShowSuccess("¡Mensaje!", "Registro actualizado correctamente");

            await LoadAsync();

            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _dialogService.ShowError("Clientes", StatusMessage);
        }
    }

    /// <summary>
    /// Confirma y registra el pago de una cuota vencida en el plan seleccionado.
    /// Actualiza fecha de pago y cobrador, pero no extiende la vigencia del contrato.
    /// </summary>
    /// <returns>Una tarea que termina al guardar el plan o mostrar un error.</returns>
    private async Task MarkMonthlyFeePaidAsync()
    {
        // La acción requiere una ficha seleccionada para evitar pagos ambiguos.
        if (Id <= 0)
        {
            _dialogService.ShowWarning("Mensualidad", "Seleccione un vehículo mensualizado.");
            return;
        }

        try
        {
            // Se vuelve a consultar antes de pagar para usar el estado más reciente.
            var vehicle = await _service.GetByIdAsync(Id);
            if (vehicle?.vehicle_monthly_plan == null)
                throw new InvalidOperationException("No se encontró el contrato mensual.");

            // La misma política usada al ingresar determina si existe cuota vencida.
            decimal pending = MonthlyAccessPolicy.Evaluate(vehicle, DateTime.Now).PendingFee;
            if (pending <= 0)
            {
                _dialogService.ShowInfo("Mensualidad", "Este contrato no tiene una cuota vencida pendiente.");
                return;
            }

            // Antes de confirmar, se muestra el medio elegido y se aclara que la vigencia no cambia.
            string methodLabel = SelectedMonthlyPaymentMethod switch
            {
                "transfer" => "transferencia",
                "card" => "tarjeta",
                _ => "efectivo"
            };
            if (!_dialogService.ShowConfirmation("Registrar pago mensual",
                $"¿Confirmas que recibiste {CurrencyDisplay.Format(pending)} de {vehicle.plate} por {methodLabel}? El cobro quedará en Caja. La fecha final y el estado del contrato no cambiarán."))
                return;

            // El asiento y la fecha de pago se confirman juntos; ni estado ni fechas de vigencia cambian.
            MonthlyFeeReceipt receipt = await _monthlyLedger.RecordOverdueAsync(
                vehicle.vehicle_monthly_plan.id, _currentUserId, SelectedMonthlyPaymentMethod);

            // Se confirma de inmediato: un fallo posterior de refresco no debe parecer un cobro fallido.
            PendingMonthlyFee = 0;
            _dialogService.ShowSuccess("Mensualidad", $"Cobro #{receipt.Id} por {CurrencyDisplay.Format(receipt.Amount)} guardado en Caja. El contrato conserva su fecha de fin: amplíela manualmente si el cliente continuará, o desactívelo si no renovará.");
            try
            {
                // La lista y el formulario se renuevan después de informar el cobro confirmado.
                await LoadAsync();
                ClearForm();
            }
            catch (Exception refreshError)
            {
                _dialogService.ShowWarning("Mensualidad", $"El cobro #{receipt.Id} sí se guardó. No se pudo actualizar la lista: {refreshError.Message}");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Mensualidad", ex.Message);
        }
    }

    /// <summary>Elimina lógicamente el cliente seleccionado después de confirmar la operación.</summary>
    private async Task DeleteAsync()
    {
        try
        {
            if (Id == 0)
            {
                _dialogService.ShowWarning("¡Alerta!", "Seleccione un registro.");
                return;
            }

            var entity = await _service.GetByIdAsync(Id);

            if (entity == null)
            {
                _dialogService.ShowError("¡Mensaje!", "Registro no encontrado.");
                return;
            }

            if (!_dialogService.ShowConfirmation("Confirmar Eliminación",
                $"¿Está seguro de eliminar a: '{entity.owner_name}'?"))
            {
                return;
            }

            await _service.DeleteAsync(entity, _currentUserId);

            await LoadAsync();

            ClearForm();
            _dialogService.ShowInfo("¡Mensaje!", "El registro se eliminó correctamente.");

        }
        catch (Exception ex)
        {
            _dialogService.ShowError("¡Eror!", ex.Message);
        }
    }

    /// <summary>Restablece la ficha para crear otro cliente y elimina la deuda visible anterior.</summary>
    private void ClearForm()
    {
        // El saldo de la selección previa no debe aparecer en un cliente nuevo.
        PendingMonthlyFee = 0;
        Id = 0;

        Plate = string.Empty;

        VehicleTypeId = 0;

        OwnerName = string.Empty;

        OwnerPhone = string.Empty;

        OwnerEmail = string.Empty;

        OwnerCedula = string.Empty;

        MonthlyFee = 0;
        SelectedMonthlyPaymentMethod = "cash";

        MonthlyStartDate = DateTime.Today;

        MonthlyEndDate = DateTime.Today.AddMonths(1);

        Notes = string.Empty;

        IsActive = true;

        SelectedRegisteredVehicle = null;

        InitializeSchedules();
    }
}
