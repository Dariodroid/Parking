using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Datos y posiciones persistidas que necesita el tablero de puestos.</summary>
public interface IParkingDashboardService
{
    Task<IEnumerable<ParkingSlotDashboardItemDTO>> GetDashboardSlotsAsync();
    Task<Dictionary<int, (int X, int Y)>> GetSlotPositionsAsync();
    Task UpdateSlotPositionsAsync(Dictionary<int, (int X, int Y)> positions);
}
