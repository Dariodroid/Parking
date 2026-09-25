using Parking.Domain.Model.Models;

namespace Parking.Domain.Model.Abstractions;

public interface IParkingSlotRepository
    : IBaseRepository<parking_slot>
{
    Task<IEnumerable<parking_slot>> GetAvailableSlotsAsync();

    Task<parking_slot?> GetFirstAvailableSlotAsync();

    Task<parking_slot?> GetAvailableSlotByIdAsync(int id);
}
