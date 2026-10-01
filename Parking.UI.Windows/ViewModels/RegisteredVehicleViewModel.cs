using Parking.UI.Windows.Interfaces;
using Parking.Application.Services;
using Parking.Application.UseCases;
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
using System.Drawing;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class RegisteredVehicleViewModel : BaseViewModel
{
    private readonly IDialogService _dialogService;
    private readonly IRegisteredVehicleManagementService _repository;
    private readonly IMonthlyFeeLedgerService _monthlyLedger;
    bool confirmed;


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
    /// <param name="repository">Consulta y guarda vehículo, plan y horarios.</param>
    /// <param name="dialogService">Muestra confirmaciones, avisos y errores al operador.</param>
    /// <param name="monthlyLedger">Confirma el pago y guarda su asiento sin alterar la vigencia.</param>
    public RegisteredVehicleViewModel(IRegisteredVehicleManagementService repository, IDialogService dialogService,
        IMonthlyFeeLedgerService monthlyLedger)
    {
        _dialogService = dialogService;
        _repository = repository;
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

        VehicleSchedules.Add(new VehicleScheduleItemViewModel
        {
            DayOfWeek = 1,
            DayName = "LUNES",
            IsEnabled = true,
            StartTime = new TimeSpan(7, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsFullDay = false
        });

        VehicleSchedules.Add(new VehicleScheduleItemViewModel
        {
            DayOfWeek = 2,
            DayName = "MARTES",
            IsEnabled = true,
            StartTime = new TimeSpan(7, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsFullDay = false
        });

        VehicleSchedules.Add(new VehicleScheduleItemViewModel
        {
            DayOfWeek = 3,
            DayName = "MIÉRCOLES",
            IsEnabled = true,
            StartTime = new TimeSpan(7, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsFullDay = false
        });

        VehicleSchedules.Add(new VehicleScheduleItemViewModel
        {
            DayOfWeek = 4,
            DayName = "JUEVES",
            IsEnabled = true,
            StartTime = new TimeSpan(7, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsFullDay = false
        });

        VehicleSchedules.Add(new VehicleScheduleItemViewModel
        {
            DayOfWeek = 5,
            DayName = "VIERNES",
            IsEnabled = true,
            StartTime = new TimeSpan(7, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            IsFullDay = false
        });

        VehicleSchedules.Add(new VehicleScheduleItemViewModel
        {
            DayOfWeek = 6,
            DayName = "SÁBADO",
            IsEnabled = true,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            IsFullDay = false
        });

        VehicleSchedules.Add(new VehicleScheduleItemViewModel
        {
            DayOfWeek = 0,
            DayName = "DOMINGO",
            IsEnabled = false,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            IsFullDay = false
        });
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

        var items = await _repository.GetVehicleTypesAsync();

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

        var items = await _repository.GetAllCompleteAsync();

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

        // =========================
        // PLAN
        // =========================

        if (item.vehicle_monthly_plan != null)
        {
            MonthlyFee =
                item.vehicle_monthly_plan.monthly_fee;

            MonthlyStartDate =
                item.vehicle_monthly_plan.start_date;

            MonthlyEndDate =
                item.vehicle_monthly_plan.end_date;
        }

        // =========================
        // RESETEAR HORARIOS
        // =========================

        foreach (var schedule in VehicleSchedules)
        {
            schedule.IsEnabled = false;

            schedule.IsFullDay = false;

            schedule.StartTime =
                new TimeSpan(7, 0, 0);

            schedule.EndTime =
                new TimeSpan(19, 0, 0);
        }

        // =========================
        // CARGAR HORARIOS REALES
        // =========================

        if (item.monthly_vehicle_schedules != null
            && item.monthly_vehicle_schedules.Any())
        {
            foreach (var schedule
                in VehicleSchedules)
            {
                var dbSchedule =
                    item.monthly_vehicle_schedules
                        .Where(x => !x.is_deleted)
                        .FirstOrDefault(x =>
                            x.day_of_week ==
                            schedule.DayOfWeek);

                if (dbSchedule != null)
                {
                    // Una fila guardada pero desactivada debe verse desactivada;
                    // su mera existencia no autoriza acceso ese día.
                    schedule.IsEnabled = dbSchedule.is_active;

                    schedule.StartTime =
                        dbSchedule.start_time
                            .ToTimeSpan();

                    schedule.EndTime =
                        dbSchedule.end_time
                            .ToTimeSpan();

                    schedule.IsFullDay =
                        dbSchedule.is_full_day;
                }
            }
        }
    }

    /// <summary>Comprueba la ficha y las fechas antes de crear o actualizar un contrato.</summary>
    /// <returns>Verdadero cuando los datos son aptos para guardarse sin redondear importes.</returns>
    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Plate))
        {
            _dialogService.ShowWarning("Atención", "Ingrese la placa."); return false;
        }

        if (VehicleTypeId <= 0)
        {
            _dialogService.ShowWarning("Atención", "Seleccione el tipo de vehículo."); return false;
        }

        if (MonthlyFee == null || MonthlyFee <= 0)
        {
            _dialogService.ShowWarning("Atención", "Ingrese el valor mensual."); return false;
        }

        if (!MoneyAmount.IsValid(MonthlyFee.Value))
        {
            _dialogService.ShowWarning("Atención", "La mensualidad debe tener como máximo dos decimales y caber en la base de datos. No se redondeará automáticamente.");
            return false;
        }

        if (MonthlyStartDate == null)
        {
            _dialogService.ShowWarning("Atención", "Seleccione fecha inicial."); return false;
        }

        if (MonthlyEndDate == null)
        {
            _dialogService.ShowWarning("Atención", "Seleccione fecha final."); return false;
        }

        if (MonthlyEndDate < MonthlyStartDate)
        {
            _dialogService.ShowWarning("Atención", "La fecha final no puede ser menor."); return false;
        }

        return true;
    }

    /// <summary>Crea vehículo, plan y horarios; deja pendiente la cuota de un contrato creado ya vencido.</summary>
    private async Task SaveAsync()
    {
        try
        {
            if (!Validate())
                return;

            var exists =
                await _repository.ExistsByPlateAsync(
                    Plate.Trim().ToUpper());

            if (exists)
            {
                _dialogService.ShowWarning("Atención", "La placa ya existe.");
                return;
            }

            // =========================
            // 1. GUARDAR VEHÍCULO
            // =========================

            var vehicle = new registered_vehicle
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

            // Un contrato histórico que se crea ya vencido debe conservar una cuota pendiente.
            // Usamos su inicio como última fecha conocida del período anterior; no declaramos un cobro nuevo.
            DateTime startDate = MonthlyStartDate!.Value;
            DateTime endDate = MonthlyEndDate!.Value;
            bool createdAlreadyExpired = endDate.Date < DateTime.Today;

            // =========================
            // 2. GUARDAR PLAN
            // =========================

            var plan = new vehicle_monthly_plan
            {
                registered_vehicle_id = 0,

                monthly_fee =
                    MonthlyFee ?? 0,

                start_date = startDate,

                end_date = endDate,

                payment_date = createdAlreadyExpired ? startDate : DateTime.Now,

                is_active = IsActive,

                notes = Notes?.Trim(),

                created_at = DateTime.Now,

                created_by = _currentUserId,

                is_deleted = false,

                status = IsActive
                    ? "active"
                    : "cancelled",

                collected_by = createdAlreadyExpired ? null : _currentUserId
            };

            // =========================
            // 3. GUARDAR HORARIOS
            // =========================

            var schedules =
                VehicleSchedules
                .Where(x => x.IsEnabled)
                .Select(x =>
                    new monthly_vehicle_schedule
                    {
                        registered_vehicle_id = 0,

                        day_of_week =
                            x.DayOfWeek,

                        start_time =
                            TimeOnly.FromTimeSpan(
                                x.StartTime),

                        end_time =
                            TimeOnly.FromTimeSpan(
                                x.EndTime),

                        is_active = true,

                        is_full_day =
                            x.IsFullDay,

                        created_at =
                            DateTime.Now,

                        created_by =
                            _currentUserId,

                        is_deleted = false
                    })
                .ToList();

            await _repository.RegisterAsync(vehicle, plan, schedules);

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

            var entity = await _repository.GetByIdAsync(Id);

            if (entity == null)
            {

                _dialogService.ShowWarning("Atención", "Registro no encontrado.");
                return;
            }

            // =========================
            // VEHÍCULO
            // =========================

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

            // =========================
            // PLAN
            // =========================

            if (entity.vehicle_monthly_plan != null)
            {
                entity.vehicle_monthly_plan.monthly_fee = MonthlyFee ?? 0;

                entity.vehicle_monthly_plan.start_date = MonthlyStartDate ?? DateTime.Today;

                entity.vehicle_monthly_plan.end_date = MonthlyEndDate ?? DateTime.Today.AddMonths(1);

                entity.vehicle_monthly_plan.is_active = IsActive;

                entity.vehicle_monthly_plan.notes = Notes?.Trim();

                entity.vehicle_monthly_plan.updated_at = DateTime.Now;

                entity.vehicle_monthly_plan.updated_by = _currentUserId;

                entity.vehicle_monthly_plan.status = IsActive ? "active" : "cancelled";
            }

            // =========================
            // ACTUALIZAR HORARIOS
            // =========================

            // =========================
            // ACTUALIZAR HORARIOS
            // =========================

            var schedulesToUpdate = new List<monthly_vehicle_schedule>();

            var schedulesToAdd = new List<monthly_vehicle_schedule>();

            foreach (var vmSchedule in VehicleSchedules)
            {
                var existingSchedule = entity.monthly_vehicle_schedules.FirstOrDefault(x => x.day_of_week == vmSchedule.DayOfWeek);

                // =========================
                // UPDATE
                // =========================

                if (existingSchedule != null)
                {
                    existingSchedule.start_time = TimeOnly.FromTimeSpan(vmSchedule.StartTime);

                    existingSchedule.end_time =TimeOnly.FromTimeSpan(vmSchedule.EndTime);

                    existingSchedule.is_full_day = vmSchedule.IsFullDay;

                    existingSchedule.is_active = vmSchedule.IsEnabled;

                    existingSchedule.updated_at = DateTime.Now;

                    existingSchedule.updated_by = _currentUserId;

                    schedulesToUpdate .Add(existingSchedule);
                }

                // =========================
                // INSERT
                // =========================

                else if (vmSchedule.IsEnabled)
                {
                    schedulesToAdd.Add(new monthly_vehicle_schedule
                        {
                            registered_vehicle_id = entity.id,

                            day_of_week = vmSchedule.DayOfWeek,

                            start_time = TimeOnly.FromTimeSpan(vmSchedule.StartTime),

                            end_time = TimeOnly.FromTimeSpan(vmSchedule.EndTime),

                            is_active = true,

                            is_full_day = vmSchedule.IsFullDay,

                            created_at = DateTime.Now,

                            created_by = _currentUserId,

                            is_deleted = false
                        });
                }
            }

            await _repository.UpdateAsync(entity, schedulesToUpdate, schedulesToAdd);

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
            var vehicle = await _repository.GetByIdAsync(Id);
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

            var entity = await _repository.GetByIdAsync(Id);

            if (entity == null)
            {
                _dialogService.ShowError("¡Mensaje!", "Registro no encontrado.");
                return;
            }

            confirmed = _dialogService.ShowConfirmation("Confirmar Eliminación",
            $"¿Está seguro de eliminar a: '{entity.owner_name}'?");

            if (!confirmed)
            {
                return;
            }

            await _repository.DeleteAsync(entity, _currentUserId);

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
