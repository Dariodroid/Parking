using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Consulta y organiza los puestos que aparecen en el tablero.</summary>
public interface IParkingDashboard
{
    Task<IEnumerable<ParkingSlotDashboardItemDTO>> GetDashboardSlotsAsync();
    Task<Dictionary<int, (int X, int Y)>> GetSlotPositionsAsync();
    Task UpdateSlotPositionsAsync(Dictionary<int, (int X, int Y)> positions);
}
