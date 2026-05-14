using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Parking.UI.Windows.View.Dialogs;

namespace Parking.UI.Windows.ViewModels;

public class vehicle_typeViewModel : BaseViewModel
{
    private readonly Ivehicle_typeRepository _repository;

    private readonly int _currentuserId = 1;

    public ObservableCollection<vehicle_type> vehicle_types { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand DeleteCommand { get; }

    public vehicle_typeViewModel(Ivehicle_typeRepository repository)
    {
        _repository = repository;

        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        UpdateCommand = new RelayCommand(async _ => await UpdateAsync());
        NewCommand = new RelayCommand(_ => ClearForm());
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync());
    }

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
        set
        {
            if (value < 0)
                value = 0;

            SetProperty(ref _hourlyRate, Math.Round(value, 2));
        }
    }

    private int _graceMinutes = 5;
    public int GraceMinutes
    {
        get => _graceMinutes;
        set
        {
            if (value < 0)
                value = 0;

            SetProperty(ref _graceMinutes, value);
        }
    }

    private int _fractionMinutes = 15;
    public int FractionMinutes
    {
        get => _fractionMinutes;
        set
        {
            if (value < 0)
                value = 0;

            SetProperty(ref _fractionMinutes, value);
        }
    }

    private decimal _fractionRate;
    public decimal FractionRate
    {
        get => _fractionRate;
        set
        {
            if (value < 0)
                value = 0;

            SetProperty(ref _fractionRate, Math.Round(value, 2));
        }
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

    private vehicle_type? _selectedvehicle_type;
    public vehicle_type? Selectedvehicle_type
    {
        get => _selectedvehicle_type;
        set
        {
            if (SetProperty(ref _selectedvehicle_type, value) && value != null)
            {
                LoadSelected(value);
            }
        }
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
        vehicle_types.Clear();

        var items = await _repository.GetAllAsync();

        foreach (var item in items)
        {
            vehicle_types.Add(item);
        }
    }

    private void LoadSelected(vehicle_type item)
    {
        Id = item.id;

        Name = item.name ?? string.Empty;
        Icon = item.icon ?? string.Empty;

        HourlyRate = item.hourly_rate;

        GraceMinutes = item.grace_minutes;

        FractionMinutes = item.fraction_minutes;

        FractionRate = item.fraction_rate;

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
            StatusMessage = "La tarifa por hora debe ser mayor que cero.";
            return false;
        }

        if (FractionMinutes <= 0)
        {
            StatusMessage = "Los minutos por fracción deben ser mayores a cero.";
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
                StatusMessage = "Use NUEVO antes de guardar.";
                return;
            }

            var entity = new vehicle_type
            {
                name = Name.Trim(),
                icon = Icon?.Trim(),

                hourly_rate = HourlyRate,

                grace_minutes = GraceMinutes,
                fraction_minutes = FractionMinutes,
                fraction_rate = FractionRate,

                is_active = IsActive,

                created_at = DateTime.Now,
                created_by = _currentuserId,

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
                StatusMessage = "Seleccione un registro.";
                return;
            }

            var entity = await _repository.GetByIdAsync(Id);

            if (entity == null)
            {
                StatusMessage = "Registro no encontrado.";
                return;
            }

            entity.name = Name.Trim();
            entity.icon = Icon?.Trim();

            entity.hourly_rate = HourlyRate;

            entity.grace_minutes = GraceMinutes;
            entity.fraction_minutes = FractionMinutes;
            entity.fraction_rate = FractionRate;

            entity.is_active = IsActive;

            entity.updated_at = DateTime.Now;
            entity.updated_by = _currentuserId;

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

            var owner = System.Windows.Application.Current.MainWindow;
            var dialog = new ConfirmDialog(
                owner,
                $"¿Está seguro de eliminar el tipo de vehículo '{entity.name}'?");
            var result = dialog.ShowDialog();

            if (result != true)
            {
                StatusMessage = "Eliminación cancelada.";
                return;
            }

            entity.is_deleted = true;
            entity.deleted_at = DateTime.Now;
            entity.deleted_by = _currentuserId;

            await _repository.SoftDeleteAsync(entity);

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

        Name = string.Empty;
        Icon = string.Empty;

        HourlyRate = 0;

        GraceMinutes = 5;

        FractionMinutes = 15;

        FractionRate = 0;

        IsActive = true;

        Selectedvehicle_type = null;
    }
}