using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class ParkingSlotViewModel : BaseViewModel
{
    private readonly IParkingSlotRepository _repository;

    public ObservableCollection<parking_slot> Slots { get; }
        = new();

    public ICommand SaveCommand { get; }

    public ICommand UpdateCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand NewCommand { get; }

    public ParkingSlotViewModel(
        IParkingSlotRepository repository)
    {
        _repository = repository;

        SaveCommand =
            new RelayCommand(async _ =>
                await SaveAsync());

        UpdateCommand =
            new RelayCommand(async _ =>
                await UpdateAsync());

        DeleteCommand =
            new RelayCommand(async _ =>
                await DeleteAsync());

        NewCommand =
            new RelayCommand(_ =>
                ClearForm());
        InitializeAsync();
    }

    #region PROPERTIES

    private int _id;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _slotNumber;

    public string SlotNumber
    {
        get => _slotNumber;
        set => SetProperty(ref _slotNumber, value);
    }

    private string _statusMessage;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private parking_slot _selectedSlot;

    public parking_slot SelectedSlot
    {
        get => _selectedSlot;
        set
        {
            if (SetProperty(ref _selectedSlot, value)
                && value != null)
            {
                LoadSelected(value);
            }
        }
    }

    #endregion

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Slots.Clear();

        var items =
            await _repository.GetAllAsync();

        //MessageBox.Show(
        //    $"Registros encontrados: {items.Count()}");

        foreach (var item in items)
        {
            Slots.Add(item);
        }
    }

    private void LoadSelected(
        parking_slot slot)
    {
        Id = slot.id;

        SlotNumber = slot.slot_number;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(
                SlotNumber))
            {
                StatusMessage =
                    "Ingrese el puesto.";

                return;
            }

            await _repository.AddAsync(
                new parking_slot
                {
                    slot_number =
                        SlotNumber.Trim(),

                    is_occupied = false,

                    updated_at = DateTime.Now
                });

            await _repository.SaveChangesAsync();

            StatusMessage =
                "Puesto creado correctamente.";

            await LoadAsync();

            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage =
                ex.Message;
        }
    }

    private async Task UpdateAsync()
    {
        try
        {
            if (Id == 0)
            {
                StatusMessage =
                    "Seleccione un puesto.";

                return;
            }

            var slot =
                await _repository
                    .GetByIdAsync(Id);

            if (slot == null)
            {
                StatusMessage =
                    "Puesto no encontrado.";

                return;
            }

            slot.slot_number =
                SlotNumber.Trim();

            slot.updated_at =
                DateTime.Now;

            await _repository.UpdateAsync(slot);

            await _repository.SaveChangesAsync();

            StatusMessage =
                "Puesto actualizado correctamente.";

            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage =
                ex.Message;
        }
    }

    private async Task DeleteAsync()
    {
        try
        {
            if (Id == 0)
            {
                StatusMessage =
                    "Seleccione un puesto.";

                return;
            }

            var slot =
                await _repository
                    .GetByIdAsync(Id);

            if (slot == null)
            {
                StatusMessage =
                    "Puesto no encontrado.";

                return;
            }

            if (slot.is_occupied)
            {
                StatusMessage =
                    "No puede eliminar un puesto ocupado.";

                return;
            }

            await _repository.DeleteAsync(slot.id);

            await _repository.SaveChangesAsync();

            StatusMessage =
                "Puesto eliminado correctamente.";

            await LoadAsync();

            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage =
                ex.Message;
        }
    }

    private void ClearForm()
    {
        Id = 0;

        SlotNumber = string.Empty;

        SelectedSlot = null;
    }
}