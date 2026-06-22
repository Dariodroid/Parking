using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly IParkingDashboard _repository;

    public ICommand SelectSlotCommand { get; }

    public ObservableCollection< ParkingSlotDashboardItemDTO> Slots {get;} = new();

    public DashboardViewModel(IParkingDashboard repository)
    {
        _repository = repository;
        SelectSlotCommand =
            new RelayCommand(slot =>
            {
                SelectedSlot =
                    slot as ParkingSlotDashboardItemDTO;
            });
        InitializeAsync();
    }

    #region PROPIEDADES

    private ParkingSlotDashboardItemDTO
        _selectedSlot;

    public ParkingSlotDashboardItemDTO
        SelectedSlot
    {
        get => _selectedSlot;
        set => SetProperty(
            ref _selectedSlot,
            value);
    }

    private int _totalSlots;

    public int TotalSlots
    {
        get => _totalSlots;
        set => SetProperty(
            ref _totalSlots,
            value);
    }

    private int _occupiedSlots;

    public int OccupiedSlots
    {
        get => _occupiedSlots;
        set => SetProperty(
            ref _occupiedSlots,
            value);
    }

    private int _freeSlots;

    public int FreeSlots
    {
        get => _freeSlots;
        set => SetProperty(
            ref _freeSlots,
            value);
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
            await _repository
                .GetDashboardSlotsAsync();

        foreach (var item in items
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.SlotNumber))
        {
            Slots.Add(item);
        }

        TotalSlots =
            Slots.Count;

        OccupiedSlots =
            Slots.Count(x =>
                x.IsOccupied);

        FreeSlots =
            Slots.Count(x =>
                !x.IsOccupied);
    }
}