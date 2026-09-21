using Parking.Application.Services;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.View.Dialogs;
using Parking.UI.Windows.Helpers;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class RegisteredVehicleViewModel : BaseViewModel
{
    private readonly IDialogService _dialogService;
    private readonly IRegisteredVehicle _repository;
    bool confirmed;

    private readonly IBaseRepository<vehicle_type> _vehicleTypeRepository;

    private readonly int _currentUserId = CurrentUser.Id;

    public ObservableCollection<registered_vehicle> RegisteredVehicles { get; } = new();

    public ObservableCollection<vehicle_type> VehicleTypes { get; } = new();

    public ObservableCollection<VehicleScheduleItemViewModel> VehicleSchedules { get; } = new();

    public ICommand SaveCommand { get; }

    public ICommand UpdateCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand NewCommand { get; }

    public RegisteredVehicleViewModel(IRegisteredVehicle repository, IDialogService dialogService, IBaseRepository<vehicle_type> vehicleTypeRepository)
    {
        _dialogService = dialogService;
        _repository = repository;

        _vehicleTypeRepository = vehicleTypeRepository;

        SaveCommand = new RelayCommand(async _ => await SaveAsync());

        UpdateCommand = new RelayCommand(async _ => await UpdateAsync());

        DeleteCommand = new RelayCommand(async _ => await DeleteAsync());

        NewCommand = new RelayCommand(_ => ClearForm());

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

    public async Task InitializeAsync()
    {
        if (_isLoaded)
            return;

        _isLoaded = true;

        await LoadVehicleTypesAsync();

        InitializeSchedules();

        await LoadAsync();
    }

    private async Task LoadVehicleTypesAsync()
    {
        VehicleTypes.Clear();

        var items = await _vehicleTypeRepository.GetAllAsync();

        foreach (var item in items)
        {
            if (!item.is_deleted)
            {
                VehicleTypes.Add(item);
            }
        }
    }

    private async Task LoadAsync()
    {
        RegisteredVehicles.Clear();

        var items = await _repository.GetAllCompleteAsync();

        foreach (var item in items)
        {
            RegisteredVehicles.Add(item);
        }
    }

    private void LoadSelected(registered_vehicle item)
    {
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
                    schedule.IsEnabled = true;

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

            await _repository.AddAsync(vehicle);

            await _repository.SaveChangesAsync();

            // YA TENEMOS EL ID
            int registeredVehicleId = vehicle.id;

            // =========================
            // 2. GUARDAR PLAN
            // =========================

            var plan = new vehicle_monthly_plan
            {
                registered_vehicle_id =
                    registeredVehicleId,

                monthly_fee =
                    MonthlyFee ?? 0,

                start_date =
                    MonthlyStartDate ??
                    DateTime.Today,

                end_date =
                    MonthlyEndDate ??
                    DateTime.Today.AddMonths(1),

                payment_date = DateTime.Now,

                is_active = IsActive,

                notes = Notes?.Trim(),

                created_at = DateTime.Now,

                created_by = _currentUserId,

                is_deleted = false,

                status = IsActive
                    ? "active"
                    : "cancelled",

                collected_by = _currentUserId
            };

            await _repository
                .AddMonthlyPlanAsync(plan);

            await _repository.SaveChangesAsync();

            // =========================
            // 3. GUARDAR HORARIOS
            // =========================

            var schedules =
                VehicleSchedules
                .Where(x => x.IsEnabled)
                .Select(x =>
                    new monthly_vehicle_schedule
                    {
                        registered_vehicle_id =
                            registeredVehicleId,

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

            await _repository
                .AddSchedulesAsync(schedules);

            await _repository.SaveChangesAsync();

            StatusMessage =
                "Cliente mensualizado registrado.";

            await LoadAsync();

            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

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

            var entity = await _repository .GetCompleteByIdAsync(Id);

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

            // =========================
            // GUARDAR UPDATES
            // =========================

            if (schedulesToUpdate.Any())
            {
                await _repository.UpdateSchedulesAsync(schedulesToUpdate);
            }

            // =========================
            // GUARDAR NUEVOS
            // =========================

            if (schedulesToAdd.Any())
            {
                await _repository.AddSchedulesAsync(schedulesToAdd);
            }
            // =========================
            // GUARDAR TODO
            // =========================

            await _repository.UpdateAsync(entity);

            await _repository.SaveChangesAsync();

            _dialogService.ShowSuccess("¡Mensaje!", "Registro actualizado correctamente");

            await LoadAsync();

            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task DeleteAsync()
    {
        try
        {
            if (Id == 0)
            {
                _dialogService.ShowWarning("¡Alerta!", "Seleccione un registro.");
                return;
            }

            var entity = await _repository.GetCompleteByIdAsync(Id);

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

            await _repository
                .SoftDeleteAsync(
                    entity,
                    _currentUserId);

            await _repository.SaveChangesAsync();

            await LoadAsync();

            ClearForm();
            _dialogService.ShowInfo("¡Mensaje!", "El registro se eliminó correctamente.");

        }
        catch (Exception ex)
        {
            _dialogService.ShowError("¡Eror!", ex.Message);
        }
    }

    private void ClearForm()
    {
        Id = 0;

        Plate = string.Empty;

        VehicleTypeId = 0;

        OwnerName = string.Empty;

        OwnerPhone = string.Empty;

        OwnerEmail = string.Empty;

        OwnerCedula = string.Empty;

        MonthlyFee = 0;

        MonthlyStartDate = DateTime.Today;

        MonthlyEndDate = DateTime.Today.AddMonths(1);

        Notes = string.Empty;

        IsActive = true;

        SelectedRegisteredVehicle = null;

        InitializeSchedules();
    }
}
