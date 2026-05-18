using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class RegisteredVehicleViewModel : BaseViewModel
{
    private readonly IRegisteredVehicle _repository;
    private readonly IBaseRepository<vehicle_type> _vehicleTypeRepository;

    private readonly int _currentUserId = 1;

    public ObservableCollection<registered_vehicle> RegisteredVehicles { get; } = new();
    public ObservableCollection<vehicle_type> VehicleTypes { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand NewCommand { get; }

    public RegisteredVehicleViewModel(
        IRegisteredVehicle repository,
        IBaseRepository<vehicle_type> vehicleTypeRepository)
    {
        _repository = repository;
        _vehicleTypeRepository = vehicleTypeRepository;

        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        UpdateCommand = new RelayCommand(async _ => await UpdateAsync());
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync());
        NewCommand = new RelayCommand(_ => ClearForm());
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
        set => SetProperty(ref _plate, value.ToUpper());
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
            if (value < 0) value = 0;
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
            if (SetProperty(ref _selectedRegisteredVehicle, value) && value != null)
                LoadSelected(value);
        }
    }

    #endregion

    private bool _isLoaded;

    public async Task InitializeAsync()
    {
        if (_isLoaded) return;

        _isLoaded = true;

        await LoadVehicleTypesAsync();
        await LoadAsync();
    }

    private async Task LoadVehicleTypesAsync()
    {
        VehicleTypes.Clear();

        var items = await _vehicleTypeRepository.GetAllAsync();

        foreach (var item in items)
        {
            if (!item.is_deleted)
                VehicleTypes.Add(item);
        }
    }

    private async Task LoadAsync()
    {
        RegisteredVehicles.Clear();

        var items = await _repository.GetAllAsync();

        foreach (var item in items)
        {
            if (!item.is_deleted)
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

        MonthlyFee = item.monthly_fee;
        MonthlyStartDate = item.monthly_start_date;
        MonthlyEndDate = item.monthly_end_date;

        Notes = item.notes ?? string.Empty;
        IsActive = item.is_active;
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Plate))
        {
            StatusMessage = "Ingrese la placa.";
            return false;
        }

        if (VehicleTypeId <= 0)
        {
            StatusMessage = "Seleccione el tipo de vehículo.";
            return false;
        }

        if (MonthlyFee == null || MonthlyFee <= 0)
        {
            StatusMessage = "Ingrese la mensualidad.";
            return false;
        }

        if (MonthlyStartDate == null)
        {
            StatusMessage = "Seleccione la fecha inicial.";
            return false;
        }

        if (MonthlyEndDate == null)
        {
            StatusMessage = "Seleccione la fecha final.";
            return false;
        }

        if (MonthlyEndDate < MonthlyStartDate)
        {
            StatusMessage = "La fecha final no puede ser menor.";
            return false;
        }

        return true;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (!Validate()) return;

            var exists = await _repository.ExistsByPlateAsync(Plate.Trim());

            if (exists)
            {
                StatusMessage = "La placa ya está registrada.";
                return;
            }

            var entity = new registered_vehicle
            {
                plate = Plate.Trim().ToUpper(),
                vehicle_type_id = VehicleTypeId,
                owner_name = OwnerName?.Trim(),
                owner_phone = OwnerPhone?.Trim(),
                owner_email = OwnerEmail?.Trim(),
                owner_cedula = OwnerCedula?.Trim(),
                monthly_fee = MonthlyFee,
                monthly_start_date = MonthlyStartDate,
                monthly_end_date = MonthlyEndDate,
                notes = Notes?.Trim(),
                is_active = IsActive,
                created_at = DateTime.Now,
                created_by = _currentUserId,
                is_deleted = false
            };

            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();

            StatusMessage = "Cliente mensualizado registrado.";

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
            if (!Validate()) return;

            if (Id == 0)
            {
                StatusMessage = "Seleccione un registro.";
                return;
            }

            var entity = await _repository.GetByIdAsync(Id);

            if (entity == null)
            {
                StatusMessage = "Registro no encontrado.";
                return;
            }

            entity.plate = Plate.Trim().ToUpper();
            entity.vehicle_type_id = VehicleTypeId;
            entity.owner_name = OwnerName?.Trim();
            entity.owner_phone = OwnerPhone?.Trim();
            entity.owner_email = OwnerEmail?.Trim();
            entity.owner_cedula = OwnerCedula?.Trim();
            entity.monthly_fee = MonthlyFee;
            entity.monthly_start_date = MonthlyStartDate;
            entity.monthly_end_date = MonthlyEndDate;
            entity.notes = Notes?.Trim();
            entity.is_active = IsActive;
            entity.updated_at = DateTime.Now;
            entity.updated_by = _currentUserId;

            await _repository.UpdateAsync(entity);
            await _repository.SaveChangesAsync();

            StatusMessage = "Registro actualizado.";

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
                StatusMessage = "Seleccione un registro.";
                return;
            }

            var entity = await _repository.GetByIdAsync(Id);

            if (entity == null)
            {
                StatusMessage = "Registro no encontrado.";
                return;
            }

            var result = MessageBox.Show(
                $"¿Eliminar cliente mensualizado '{entity.plate}'?",
                "Confirmación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                StatusMessage = "Operación cancelada.";
                return;
            }

            await _repository.SoftDeleteAsync(entity, _currentUserId);
            await _repository.SaveChangesAsync();

            StatusMessage = "Registro eliminado.";

            await LoadAsync();
            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
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
    }
}