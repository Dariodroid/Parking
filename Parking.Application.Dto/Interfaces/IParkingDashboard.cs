using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Application.Dto;

namespace Parking.Application.Dto.Interfaces;

public interface IParkingDashboard
{
    Task<IEnumerable<ParkingSlotDashboardItemDTO>> GetDashboardSlotsAsync();

    Task<Dictionary<int, (int X, int Y)>> GetSlotPositionsAsync();

    Task UpdateSlotPositionsAsync(Dictionary<int, (int X, int Y)> positions);
}