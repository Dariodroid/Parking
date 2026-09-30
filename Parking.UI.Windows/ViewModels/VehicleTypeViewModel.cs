using Parking.Application.Services;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.View.Dialogs;
using Parking.UI.Windows.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class vehicle_typeViewModel : BaseViewModel
{
    private readonly IDialogService _dialogService;
    private readonly Ivehicle_typeRepository _repository;
    bool dialog;

    private readonly int _currentuserId = CurrentUser.Id;

    public ObservableCollection<vehicle_type> vehicle_types { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand DeleteCommand { get; }

    /// <summary>Prepara la edición de tipos de vehículo y sus tarifas.</summary>
    /// <param name="repository">Consulta y guarda los tipos de vehículo.</param>
    /// <param name="dialogService">Muestra validaciones y resultados al operador.</param>
    public vehicle_typeViewModel(Ivehicle_typeRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;

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

            // Conservar el dato escrito permite rechazar fracciones de centavo antes de guardar.
            SetProperty(ref _hourlyRate, value);
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

            SetProperty(ref _fractionRate, value);
        }
    }

    private bool _isActive = true;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
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

    /// <summary>Carga la lista de tipos una sola vez al abrir el formulario.</summary>
    public async Task InitializeAsync()
    {
        if (_isLoaded)
            return;

        _isLoaded = true;

        await LoadAsync();
    }

    /// <summary>Recarga la lista de tipos de vehículo desde la base.</summary>
    private async Task LoadAsync()
    {
        vehicle_types.Clear();

        var items = await _repository.GetAllAsync();

        foreach (var item in items)
        {
            vehicle_types.Add(item);
        }
    }

    /// <summary>Copia la tarifa y los datos del tipo seleccionado al formulario.</summary>
    /// <param name="item">Tipo de vehículo elegido en la lista.</param>
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

    /// <summary>Valida nombres, tarifas exactas y límites antes de guardar.</summary>
    /// <returns>Verdadero si el tipo puede persistirse sin redondeo monetario.</returns>
    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            _dialogService.ShowWarning("Atención", "Debe ingresar el nombre.");
            return false;
        }

        if (HourlyRate <= 0)
        {
            _dialogService.ShowWarning("Atención", "La tarifa por hora debe ser mayor que cero.");
            return false;
        }

        if (!Parking.Application.UseCases.MoneyAmount.IsValid(HourlyRate)
            || !Parking.Application.UseCases.MoneyAmount.IsValid(FractionRate))
        {
            _dialogService.ShowWarning("Atención", "Las tarifas deben tener como máximo dos decimales y caber en la base de datos. No se redondearán automáticamente.");
            return false;
        }

        if (FractionMinutes <= 0)
        {
            _dialogService.ShowWarning("Atención", "Los minutos por fracción deben ser mayores a cero.");
            return false;
        }

        return true;
    }

    /// <summary>Crea un tipo de vehículo y actualiza la lista visible.</summary>
    private async Task SaveAsync()
    {
        try
        {
            if (!Validate())
                return;

            if (Id != 0)
            {
                _dialogService.ShowWarning("Atención", "Use NUEVO antes de guardar.");
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

            _dialogService.ShowWarning("Atención", "El nombre del tipo de vehículo es obligatorio");

            await LoadAsync();

            ClearForm();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Error", ex.Message);
        }
    }

    /// <summary>Guarda los cambios del tipo seleccionado sin crear un duplicado.</summary>
    private async Task UpdateAsync()
    {
        try
        {
            if (!Validate())
                return;

            if (Id == 0)
            {
                _dialogService.ShowWarning("Alerta !", "Seleccione un registro.");
                return;
            }

            var entity = await _repository.GetByIdAsync(Id);

            if (entity == null)
            {
                _dialogService.ShowError("", "Registro no encontrado.");
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

            _dialogService.ShowSuccess("Mensaje !","Registro actualizado correctamente.");

            await LoadAsync();

            ClearForm();
        }
        catch (Exception ex)
        {
            _dialogService.ShowInfo("Mensaje !", ex.Message);
        }
    }

    /// <summary>Elimina el tipo seleccionado tras las validaciones del repositorio.</summary>
    private async Task DeleteAsync()
    {
        try
        {
            if (Id == 0)
            {
                _dialogService.ShowWarning("Alerta", "Seleccione un registro.");
                return;
            }

            var entity = await _repository.GetByIdAsync(Id);

            if (entity == null)
            {
                _dialogService.ShowWarning("Atención", "Registro no encontrado.");
                return;
            }

            dialog = _dialogService.ShowConfirmation("Confirmación", $"¿Está seguro de eliminar el tipo de vehículo '{entity.name}'?");

            if (!dialog)
            {
                return;
            }

            entity.is_deleted = true;
            entity.deleted_at = DateTime.Now;
            entity.deleted_by = _currentuserId;

            await _repository.SoftDeleteAsync(entity);

            await _repository.SaveChangesAsync();

            _dialogService.ShowInfo("Mensaje !", "Registro eliminado correctamente.");

            await LoadAsync();

            ClearForm();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Error", ex.Message);
        }
    }
    /// <summary>Restablece el formulario y sus tarifas predeterminadas.</summary>
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
