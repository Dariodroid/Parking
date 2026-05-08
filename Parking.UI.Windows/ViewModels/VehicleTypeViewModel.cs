using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class VehicleTypeViewModel : BaseViewModel
{
    private readonly IVehicleTypeRepository _repository;

    private readonly int _currentUserId = 1;

    private int _id;
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _icon = string.Empty;
    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    private decimal _hourlyRate;
    public decimal HourlyRate
    {
        get => _hourlyRate;
        set => SetProperty(ref _hourlyRate, value);
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

    public ObservableCollection<VehicleType> VehicleTypes { get; } = new();

    private VehicleType? _selectedVehicleType;
    public VehicleType? SelectedVehicleType
    {
        get => _selectedVehicleType;
        set
        {
            if (SetProperty(ref _selectedVehicleType, value) && value != null)
            {
                LoadSelected(value);
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand DeleteCommand { get; }

    public VehicleTypeViewModel(IVehicleTypeRepository repository)
    {
        _repository = repository;

        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        UpdateCommand = new RelayCommand(async _ => await UpdateAsync());
        NewCommand = new RelayCommand(_ => ClearForm());
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync());
    }

    private bool _isLoaded;

    public async Task InitializeAsync()
    {
        if (_isLoaded)
            return;

        _isLoaded = true;

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        VehicleTypes.Clear();

        var items = await _repository.GetAllAsync();

        foreach (var item in items)
        {
            VehicleTypes.Add(item);
        }
    }

    private void LoadSelected(VehicleType item)
    {
        Id = item.Id;
        Name = item.Name ?? string.Empty;
        Icon = item.Icon ?? string.Empty;
        HourlyRate = item.HourlyRate;
        IsActive = item.is_active;
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Debe ingresar el nombre.";
            return false;
        }

        if (HourlyRate <= 0)
        {
            StatusMessage = "La tarifa debe ser mayor que cero.";
            return false;
        }

        return true;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (!Validate())
                return;

            if (Id != 0)
            {
                StatusMessage = "Para guardar un nuevo registro use NUEVO primero.";
                return;
            }

            var entity = new VehicleType
            {
                Name = Name.Trim(),
                Icon = Icon?.Trim(),
                HourlyRate = HourlyRate,
                is_active = IsActive,
                CreatedAt = DateTime.Now,
                CreatedBy = _currentUserId,
                is_deleted = false
            };

            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();

            StatusMessage = "Tipo de vehículo registrado.";

            await LoadAsync();
            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
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
                StatusMessage = "Seleccione un registro para actualizar.";
                return;
            }

            var entity = await _repository.GetByIdAsync(Id);

            if (entity == null)
            {
                StatusMessage = "Registro no encontrado.";
                return;
            }

            entity.Name = Name.Trim();
            entity.Icon = Icon?.Trim();
            entity.HourlyRate = HourlyRate;
            entity.is_active = IsActive;
            entity.UpdatedAt = DateTime.Now;
            entity.UpdatedBy = _currentUserId;

            await _repository.UpdateAsync(entity);
            await _repository.SaveChangesAsync();

            StatusMessage = "Tipo de vehículo actualizado.";

            await LoadAsync();
            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
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

            entity.is_deleted = true;
            entity.DeletedAt = DateTime.Now;
            entity.DeletedBy = _currentUserId;

            await _repository.SoftDeleteAsync(entity);
            await _repository.SaveChangesAsync();

            StatusMessage = "Registro eliminado correctamente.";

            await LoadAsync();
            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void ClearForm()
    {
        Id = 0;
        Name = string.Empty;
        Icon = string.Empty;
        HourlyRate = 0;
        IsActive = true;
        SelectedVehicleType = null;
    }
}