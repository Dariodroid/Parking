using Parking.Domain.Model.Models;

namespace Parking.Domain.Model.Abstractions;

public interface IRegisteredVehicle
    : IBaseRepository<registered_vehicle>
{
    Task<bool> ExistsByPlateAsync(
        string plate);

    Task<registered_vehicle?>
        GetCompleteByIdAsync(int id);

    /// <summary>Busca una placa e incluye el plan y los horarios necesarios para decidir su acceso.</summary>
    /// <param name="plate">Placa normalizada en mayúsculas.</param>
    /// <returns>Cliente con sus relaciones, o nulo si no está registrado o fue eliminado.</returns>
    Task<registered_vehicle?> GetCompleteByPlateAsync(string plate);

    Task<List<registered_vehicle>>
        GetAllCompleteAsync();

    Task SoftDeleteAsync(
        registered_vehicle entity,
        int deletedBy);

    Task AddMonthlyPlanAsync(
        vehicle_monthly_plan plan);

    Task AddSchedulesAsync(
        List<monthly_vehicle_schedule> schedules);

    Task UpdateSchedulesAsync(
        List<monthly_vehicle_schedule> schedules);
}
