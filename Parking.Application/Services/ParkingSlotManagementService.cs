using Parking.Application.Interfaces;
using Parking.Domain.Model.Interfaces;
using Parking.Domain.Model.Models;

namespace Parking.Application.Services;

/// <summary>Coordina altas y cambios de puestos, sin exponer el repositorio a la UI.</summary>
public sealed class ParkingSlotManagementService : IParkingSlotManagementService
{
    private readonly IParkingSlotRepository _slots;

    public ParkingSlotManagementService(IParkingSlotRepository slots) => _slots = slots;

    public Task<IEnumerable<parking_slot>> GetAllAsync() => _slots.GetAllAsync();
    public Task<parking_slot?> GetByIdAsync(long id) => _slots.GetByIdAsync(id);

    public async Task CreateAsync(parking_slot slot)
    {
        await _slots.AddAsync(slot);
        await _slots.SaveChangesAsync();
    }

    public async Task UpdateAsync(parking_slot slot)
    {
        await _slots.UpdateAsync(slot);
        await _slots.SaveChangesAsync();
    }

    public async Task DeleteAsync(parking_slot slot)
    {
        if (slot.is_occupied)
            throw new InvalidOperationException("No puede eliminar un puesto ocupado.");
        await _slots.DeleteAsync(slot.id);
        await _slots.SaveChangesAsync();
    }
}
