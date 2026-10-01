using Parking.Application.Interfaces;
using Parking.Domain.Model.Interfaces;
using Parking.Domain.Model.Models;

namespace Parking.Application.Services;

/// <summary>Confirma cada cambio de tipo de vehículo mediante su repositorio.</summary>
public sealed class VehicleTypeManagementService : IVehicleTypeManagementService
{
    private readonly Ivehicle_typeRepository _types;

    public VehicleTypeManagementService(Ivehicle_typeRepository types) => _types = types;

    public Task<IEnumerable<vehicle_type>> GetAllAsync() => _types.GetAllAsync();
    public Task<vehicle_type?> GetByIdAsync(long id) => _types.GetByIdAsync(id);

    public async Task CreateAsync(vehicle_type type)
    {
        await _types.AddAsync(type);
        await _types.SaveChangesAsync();
    }

    public async Task UpdateAsync(vehicle_type type)
    {
        await _types.UpdateAsync(type);
        await _types.SaveChangesAsync();
    }

    public async Task DeleteAsync(vehicle_type type, int operatorId)
    {
        type.is_deleted = true;
        type.deleted_at = DateTime.Now;
        type.deleted_by = operatorId;
        await _types.SoftDeleteAsync(type);
        await _types.SaveChangesAsync();
    }
}
