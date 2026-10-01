using Parking.Domain.Model.Models;

namespace Parking.Application.Interfaces;

/// <summary>Casos de uso de mantenimiento de puestos.</summary>
public interface IParkingSlotManagementService
{
    Task<IEnumerable<parking_slot>> GetAllAsync();
    Task<parking_slot?> GetByIdAsync(long id);
    Task CreateAsync(parking_slot slot);
    Task UpdateAsync(parking_slot slot);
    Task DeleteAsync(parking_slot slot);
}
