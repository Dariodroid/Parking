using Parking.Application.Interfaces;
using Parking.Domain.Model.Interfaces;
using Parking.Domain.Model.Models;

namespace Parking.Application.Services;

/// <summary>Ordena los cambios de vehículo, plan y horarios antes de confirmarlos.</summary>
public sealed class RegisteredVehicleManagementService : IRegisteredVehicleManagementService
{
    private readonly IRegisteredVehicle _vehicles;
    private readonly IBaseRepository<vehicle_type> _types;

    public RegisteredVehicleManagementService(IRegisteredVehicle vehicles, IBaseRepository<vehicle_type> types)
    {
        _vehicles = vehicles;
        _types = types;
    }

    public Task<IEnumerable<vehicle_type>> GetVehicleTypesAsync() => _types.GetAllAsync();
    public Task<List<registered_vehicle>> GetAllCompleteAsync() => _vehicles.GetAllCompleteAsync();
    public Task<registered_vehicle?> GetByIdAsync(int id) => _vehicles.GetCompleteByIdAsync(id);
    public Task<bool> ExistsByPlateAsync(string plate) => _vehicles.ExistsByPlateAsync(plate);

    /// <summary>Conserva la secuencia de altas necesaria para obtener el ID del cliente.</summary>
    public async Task RegisterAsync(registered_vehicle vehicle, vehicle_monthly_plan plan,
        List<monthly_vehicle_schedule> schedules)
    {
        await _vehicles.AddAsync(vehicle);
        await _vehicles.SaveChangesAsync();
        plan.registered_vehicle_id = vehicle.id;
        await _vehicles.AddMonthlyPlanAsync(plan);
        await _vehicles.SaveChangesAsync();
        foreach (var schedule in schedules)
            schedule.registered_vehicle_id = vehicle.id;
        await _vehicles.AddSchedulesAsync(schedules);
        await _vehicles.SaveChangesAsync();
    }

    public async Task UpdateAsync(registered_vehicle vehicle,
        List<monthly_vehicle_schedule> schedulesToUpdate,
        List<monthly_vehicle_schedule> schedulesToAdd)
    {
        if (schedulesToUpdate.Count > 0)
            await _vehicles.UpdateSchedulesAsync(schedulesToUpdate);
        if (schedulesToAdd.Count > 0)
            await _vehicles.AddSchedulesAsync(schedulesToAdd);
        await _vehicles.UpdateAsync(vehicle);
        await _vehicles.SaveChangesAsync();
    }

    public async Task DeleteAsync(registered_vehicle vehicle, int operatorId)
    {
        await _vehicles.SoftDeleteAsync(vehicle, operatorId);
        await _vehicles.SaveChangesAsync();
    }
}
