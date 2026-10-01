using Parking.Domain.Model.Models;

namespace Parking.Application.Interfaces;

/// <summary>Operaciones de administración de tarifas y tipos de vehículo.</summary>
public interface IVehicleTypeManagementService
{
    Task<IEnumerable<vehicle_type>> GetAllAsync();
    Task<vehicle_type?> GetByIdAsync(long id);
    Task CreateAsync(vehicle_type type);
    Task UpdateAsync(vehicle_type type);
    Task DeleteAsync(vehicle_type type, int operatorId);
}
