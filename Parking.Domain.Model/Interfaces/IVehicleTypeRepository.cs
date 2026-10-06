using Parking.Domain.Model.Models;

namespace Parking.Domain.Model.Interfaces;

/// <summary>Consulta tipos de vehículo y permite su eliminación lógica.</summary>
public interface IVehicleTypeRepository : IBaseRepository<vehicle_type>
{
    Task SoftDeleteAsync(vehicle_type entity);
}
