using Parking.Domain.Model.Models;

namespace Parking.Domain.Model.Abstractions;

public interface IRegisteredVehicle
    : IBaseRepository<registered_vehicle>
{
    Task<bool> ExistsByPlateAsync(string plate);

    Task<registered_vehicle?> GetCompleteByIdAsync(int id);

    Task<List<registered_vehicle>> GetAllCompleteAsync();

    Task SoftDeleteAsync(
        registered_vehicle entity,
        int deletedBy);

    Task AddMonthlyPlanAsync(
        vehicle_monthly_plan plan);

    Task AddSchedulesAsync(
        List<monthly_vehicle_schedule> schedules);

    Task RemoveSchedulesAsync(
        List<monthly_vehicle_schedule> schedules);
}