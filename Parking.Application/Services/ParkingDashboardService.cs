using Parking.Application.Dto;
using Parking.Application.Interfaces;

namespace Parking.Application.Services;

/// <summary>Ofrece al tablero los puestos y su distribución guardada.</summary>
public sealed class ParkingDashboardService : IParkingDashboardService
{
    private readonly IParkingDashboard _dashboard;

    public ParkingDashboardService(IParkingDashboard dashboard) => _dashboard = dashboard;

    /// <inheritdoc />
    public Task<IEnumerable<ParkingSlotDashboardItemDTO>> GetDashboardSlotsAsync()
        => _dashboard.GetDashboardSlotsAsync();

    /// <inheritdoc />
    public Task<Dictionary<int, (int X, int Y)>> GetSlotPositionsAsync()
        => _dashboard.GetSlotPositionsAsync();

    /// <inheritdoc />
    public Task UpdateSlotPositionsAsync(Dictionary<int, (int X, int Y)> positions)
        => _dashboard.UpdateSlotPositionsAsync(positions);
}
