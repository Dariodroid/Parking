using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly IParkingDashboard _repository;
    private readonly IParkingStatusNotifier _parkingStatusNotifier;

    public ICommand SelectSlotCommand { get; }

    // 🟢 NUEVO: El "pito del árbitro"
    public ICommand ResetLayoutCommand { get; }

    public ObservableCollection<ParkingSlotDashboardItemDTO> Slots { get; } = new();

    public DashboardViewModel(
        IParkingDashboard repository,
        IParkingStatusNotifier parkingStatusNotifier)
    {
        _repository = repository;
        _parkingStatusNotifier = parkingStatusNotifier;

        SelectSlotCommand = new RelayCommand(slot =>
        {
            SelectedSlot = slot as ParkingSlotDashboardItemDTO;
        });

        ResetLayoutCommand = new RelayCommand(async _ =>
        {
            await ResetLayoutAsync();
        });

        _parkingStatusNotifier.ParkingStatusChanged += OnParkingStatusChanged;
        _ = InitializeAsync();
    }

    private async void OnParkingStatusChanged(object? sender, EventArgs e)
    {
        try
        {
            // 🟢 CORREGIDO: Nombre completo para evitar colisión con Parking.Application
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                await LoadAsync();
                return;
            }

            // 🟢 CORREGIDO: Nombre completo para evitar colisión con Parking.Application
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(LoadAsync).Task.Unwrap();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error actualizando el dashboard: {ex.Message}");
        }
    }

    #region PROPIEDADES

    private ParkingSlotDashboardItemDTO _selectedSlot;
    public ParkingSlotDashboardItemDTO SelectedSlot
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

    // 🟢 NUEVAS PROPIEDADES: Tamaño dinámico del mapa (Canvas)
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
        var items = await _repository.GetDashboardSlotsAsync();

        // 2. Posiciones personalizadas (SQLite)
        var savedPositions = await _repository.GetSlotPositionsAsync();

        // 3. Orden profesional por número de puesto
        var orderedItems = items
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.SlotNumber)
            .ToList();

        // 4. Cuadrícula perfecta por defecto
        var defaultGrid = CalculateGridPositions(orderedItems.Count);

        for (int i = 0; i < orderedItems.Count; i++)
        {
            var item = orderedItems[i];

            if (savedPositions.TryGetValue(item.SlotId, out var savedPosition))
            {
                // 🟢 Hay diseño personalizado guardado: lo respetamos
                item.PositionX = savedPosition.X;
                item.PositionY = savedPosition.Y;
            }
            else
            {
                // 🟢 Primera vez (o puesto nuevo): cuadrícula profesional
                item.PositionX = defaultGrid[i].X;
                item.PositionY = defaultGrid[i].Y;
            }

            Slots.Add(item);
        }

        TotalSlots = Slots.Count;
        OccupiedSlots = Slots.Count(x => x.IsOccupied);
        FreeSlots = Slots.Count(x => !x.IsOccupied);

        // 🟢 NUEVO: Recalcular el tamaño del mapa al cargar
        UpdateMapSize();
    }

    /// <summary>
    /// 🟢 PITO DEL ÁRBITRO: Restaura la cuadrícula original y la guarda en SQLite.
    /// </summary>
    public async Task ResetLayoutAsync()
    {
        try
        {
            var orderedItems = Slots
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.SlotNumber)
                .ToList();

            var defaultGrid = CalculateGridPositions(orderedItems.Count);

            for (int i = 0; i < orderedItems.Count; i++)
            {
                // Gracias a INotifyPropertyChanged, la UI se actualiza en tiempo real
                orderedItems[i].PositionX = defaultGrid[i].X;
                orderedItems[i].PositionY = defaultGrid[i].Y;
            }

            // Persistimos el orden restaurado en SQLite
            await SaveSlotPositionsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error al restablecer el orden: {ex.Message}");
        }
    }

    /// <summary>
    /// Calcula las posiciones de una cuadrícula perfecta (filas y columnas uniformes).
    /// </summary>
    private static List<(int X, int Y)> CalculateGridPositions(int count)
    {
        const int cardWidth = 160;
        const int cardHeight = 100;
        const int horizontalSpacing = 20;
        const int verticalSpacing = 20;
        const int startX = 20;
        const int startY = 20;
        const int containerWidth = 1000;

        int cardsPerRow = Math.Max(1, (containerWidth - startX) / (cardWidth + horizontalSpacing));

        var result = new List<(int X, int Y)>(count);

        for (int i = 0; i < count; i++)
        {
            int row = i / cardsPerRow;
            int col = i % cardsPerRow;

            result.Add((
                startX + col * (cardWidth + horizontalSpacing),
                startY + row * (cardHeight + verticalSpacing)
            ));
        }

        return result;
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

            // 🟢 NUEVO: Recalcular el tamaño del mapa antes de persistir
            UpdateMapSize();

            await _repository.UpdateSlotPositionsAsync(positions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error guardando posiciones: {ex.Message}");
        }
    }

    /// <summary>
    /// 🟢 Calcula el tamaño real del lienzo: hasta la última tarjeta + padding.
    /// Se llama después de cualquier cambio de posiciones para que el scroll
    /// aparezca solo cuando el contenido lo requiera.
    /// </summary>
    private void UpdateMapSize()
    {
        const int cardWidth = 160;
        const int cardHeight = 100;
        const int padding = 40; // Margen de seguridad a la derecha y abajo

        if (Slots.Count == 0)
        {
            MapWidth = 0;
            MapHeight = 0;
            return;
        }

        MapWidth = Slots.Max(x => x.PositionX) + cardWidth + padding;
        MapHeight = Slots.Max(x => x.PositionY) + cardHeight + padding;
    }
}