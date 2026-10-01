using Parking.Domain.Model.Models;

namespace Parking.Application.Interfaces;

/// <summary>Casos de uso de clientes mensuales, sus planes y horarios.</summary>
public interface IRegisteredVehicleManagementService
{
    Task<IEnumerable<vehicle_type>> GetVehicleTypesAsync();
    Task<List<registered_vehicle>> GetAllCompleteAsync();
    Task<registered_vehicle?> GetByIdAsync(int id);
    Task<bool> ExistsByPlateAsync(string plate);
    Task RegisterAsync(registered_vehicle vehicle, vehicle_monthly_plan plan,
        List<monthly_vehicle_schedule> schedules);
    Task UpdateAsync(registered_vehicle vehicle, List<monthly_vehicle_schedule> schedulesToUpdate,
        List<monthly_vehicle_schedule> schedulesToAdd);
    Task DeleteAsync(registered_vehicle vehicle, int operatorId);
}
