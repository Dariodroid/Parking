using Parking.UI.Windows.Interfaces;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly IParkingDashboardService _dashboard;
    private readonly IParkingStatusNotifier _parkingStatusNotifier;

    public ICommand SelectSlotCommand { get; }

    /// <summary>Restaura la distribución inicial de los puestos.</summary>
    public ICommand ResetLayoutCommand { get; }

    public ObservableCollection<ParkingSlotDashboardItemDTO> Slots { get; } = new();

    public DashboardViewModel(
        IParkingDashboardService dashboard,
        IParkingStatusNotifier parkingStatusNotifier)
    {
        _dashboard = dashboard;
        _parkingStatusNotifier = parkingStatusNotifier;

        SelectSlotCommand = new RelayCommand(slot =>
        {
            SelectedSlot = slot as ParkingSlotDashboardItemDTO;
        });

        ResetLayoutCommand = new AsyncRelayCommand(_ => ResetLayoutAsync());

        _parkingStatusNotifier.ParkingStatusChanged += OnParkingStatusChanged;
        _ = InitializeAsync();
    }

    private async void OnParkingStatusChanged(object? sender, EventArgs e)
    {
        try
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                await LoadAsync();
                return;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(LoadAsync).Task.Unwrap();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error actualizando el dashboard: {ex.Message}");
        }
    }

    #region PROPIEDADES

    private ParkingSlotDashboardItemDTO? _selectedSlot;
    public ParkingSlotDashboardItemDTO? SelectedSlot
    {
        get => _selectedSlot;
        set => SetProperty(ref _selectedSlot, value);
    }

    private int _totalSlots;
    public int TotalSlots
    {
        get => _totalSlots;
        set => SetProperty(ref _totalSlots, value);
    }

    private int _occupiedSlots;
    public int OccupiedSlots
    {
        get => _occupiedSlots;
        set => SetProperty(ref _occupiedSlots, value);
    }

    private int _freeSlots;
    public int FreeSlots
    {
        get => _freeSlots;
        set => SetProperty(ref _freeSlots, value);
    }

    /// <summary>Ancho del lienzo que contiene los puestos.</summary>
    private double _mapWidth;
    public double MapWidth
    {
        get => _mapWidth;
        set => SetProperty(ref _mapWidth, value);
    }

    private double _mapHeight;
    public double MapHeight
    {
        get => _mapHeight;
        set => SetProperty(ref _mapHeight, value);
    }

    #endregion

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Slots.Clear();

        // 1. Datos del negocio (BD principal)
        var items = await _dashboard.GetDashboardSlotsAsync();

        // 2. Posiciones personalizadas (SQLite)
        var savedPositions = await _dashboard.GetSlotPositionsAsync();

        // 3. Orden profesional por número de puesto
        var orderedItems = items
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.SlotNumber)
            .ToList();

        // Los puestos sin posición guardada usan la cuadrícula inicial.
        var defaultGrid = DashboardGridLayout.CreatePositions(orderedItems.Count);

        for (int i = 0; i < orderedItems.Count; i++)
        {
            var item = orderedItems[i];

            if (savedPositions.TryGetValue(item.SlotId, out var savedPosition))
            {
                item.PositionX = savedPosition.X;
                item.PositionY = savedPosition.Y;
            }
            else
            {
                item.PositionX = defaultGrid[i].X;
                item.PositionY = defaultGrid[i].Y;
            }

            Slots.Add(item);
        }

        TotalSlots = Slots.Count;
        OccupiedSlots = Slots.Count(x => x.IsOccupied);
        FreeSlots = Slots.Count(x => !x.IsOccupied);

        UpdateMapSize();
    }

    /// <summary>Restaura la cuadrícula original y la guarda en SQLite.</summary>
    public async Task ResetLayoutAsync()
    {
        try
        {
            var orderedItems = Slots
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.SlotNumber)
                .ToList();

            var defaultGrid = DashboardGridLayout.CreatePositions(orderedItems.Count);

            for (int i = 0; i < orderedItems.Count; i++)
            {
                orderedItems[i].PositionX = defaultGrid[i].X;
                orderedItems[i].PositionY = defaultGrid[i].Y;
            }

            await SaveSlotPositionsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error al restablecer el orden: {ex.Message}");
        }
    }

    public async Task SaveSlotPositionsAsync()
    {
        try
        {
            var positions = new Dictionary<int, (int X, int Y)>();

            foreach (var slot in Slots)
            {
                positions[slot.SlotId] = (slot.PositionX, slot.PositionY);
            }

            UpdateMapSize();

            await _dashboard.UpdateSlotPositionsAsync(positions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error guardando posiciones: {ex.Message}");
        }
    }

    /// <summary>Ajusta el lienzo para abarcar todas las posiciones visibles.</summary>
    private void UpdateMapSize()
    {
        var size = DashboardGridLayout.GetCanvasSize(Slots);
        MapWidth = size.Width;
        MapHeight = size.Height;
    }
}
