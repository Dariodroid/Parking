using System.Collections.ObjectModel;
using System.Windows.Input;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.Interfaces;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;

namespace Parking.UI.Windows.ViewModels;

/// <summary>Presenta los puestos y coordina las acciones del formulario con Application.</summary>
public class ParkingSlotViewModel : BaseViewModel
{
    private readonly IParkingSlotManagementService _service;
    private readonly IDialogService _dialogs;

    public ObservableCollection<parking_slot> Slots { get; } = new();
    public ICommand SaveCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand NewCommand { get; }

    /// <summary>Recibe el servicio de puestos y prepara los comandos de la vista.</summary>
    public ParkingSlotViewModel(IParkingSlotManagementService service, IDialogService dialogs)
    {
        _service = service;
        _dialogs = dialogs;
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        UpdateCommand = new AsyncRelayCommand(_ => UpdateAsync());
        DeleteCommand = new AsyncRelayCommand(_ => DeleteAsync());
        NewCommand = new RelayCommand(_ => ClearForm());
        // El constructor inicia la carga; las siguientes recargas sí esperan su resultado.
        _ = InitializeAsync();
    }

    private int _id;
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _slotNumber = string.Empty;
    public string SlotNumber
    {
        get => _slotNumber;
        set => SetProperty(ref _slotNumber, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private parking_slot? _selectedSlot;
    public parking_slot? SelectedSlot
    {
        get => _selectedSlot;
        set
        {
            if (SetProperty(ref _selectedSlot, value) && value != null)
                LoadSelected(value);
        }
    }

    /// <summary>Carga los puestos cuando se abre el formulario.</summary>
    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    /// <summary>Reemplaza la lista visible con los puestos actuales.</summary>
    private async Task LoadAsync()
    {
        Slots.Clear();
        foreach (var slot in await _service.GetAllAsync())
            Slots.Add(slot);
    }

    /// <summary>Muestra el número del puesto seleccionado.</summary>
    private void LoadSelected(parking_slot slot)
    {
        Id = slot.id;
        SlotNumber = slot.slot_number;
    }

    /// <summary>Crea un puesto libre con el número introducido.</summary>
    private async Task SaveAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SlotNumber))
            {
                ShowWarning("Ingrese el puesto.");
                return;
            }

            await _service.CreateAsync(new parking_slot
            {
                slot_number = SlotNumber.Trim(),
                is_occupied = false,
                updated_at = DateTime.Now
            });
            ShowSuccess("Puesto creado correctamente.");
            await LoadAsync();
            ClearForm();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    /// <summary>Actualiza el número del puesto seleccionado.</summary>
    private async Task UpdateAsync()
    {
        try
        {
            var slot = await GetSelectedSlotAsync();
            if (slot == null) return;

            slot.slot_number = SlotNumber.Trim();
            slot.updated_at = DateTime.Now;
            await _service.UpdateAsync(slot);
            ShowSuccess("Puesto actualizado correctamente.");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    /// <summary>Elimina el puesto seleccionado solo cuando está libre.</summary>
    private async Task DeleteAsync()
    {
        try
        {
            var slot = await GetSelectedSlotAsync();
            if (slot == null) return;
            if (slot.is_occupied)
            {
                ShowWarning("No puede eliminar un puesto ocupado.");
                return;
            }

            await _service.DeleteAsync(slot);
            ShowSuccess("Puesto eliminado correctamente.");
            await LoadAsync();
            ClearForm();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    /// <summary>Busca el puesto elegido y muestra el mismo aviso si no existe.</summary>
    private async Task<parking_slot?> GetSelectedSlotAsync()
    {
        if (Id == 0)
        {
            ShowWarning("Seleccione un puesto.");
            return null;
        }
        var slot = await _service.GetByIdAsync(Id);
        if (slot == null) ShowWarning("Puesto no encontrado.");
        return slot;
    }

    /// <summary>Presenta un aviso y conserva su texto en la pantalla.</summary>
    private void ShowWarning(string message)
    {
        StatusMessage = message;
        _dialogs.ShowWarning("Puestos", message);
    }

    /// <summary>Presenta la confirmación de una operación terminada.</summary>
    private void ShowSuccess(string message)
    {
        StatusMessage = message;
        _dialogs.ShowSuccess("Puestos", message);
    }

    /// <summary>Presenta un error del servicio sin perder su detalle.</summary>
    private void ShowError(Exception ex)
    {
        StatusMessage = ex.Message;
        _dialogs.ShowError("Puestos", StatusMessage);
    }

    /// <summary>Limpia los campos para comenzar otra operación.</summary>
    private void ClearForm()
    {
        Id = 0;
        SlotNumber = string.Empty;
        SelectedSlot = null;
    }
}
