using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess.Repository;

public class RegisteredVehicleRepository
    : BaseRepository<registered_vehicle>,
      IRegisteredVehicle
{
    private readonly parking_dbContext _context;

    public RegisteredVehicleRepository(
        parking_dbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<bool> ExistsByPlateAsync(
        string plate)
    {
        return await _context.registered_vehicles
            .AnyAsync(x =>
                x.plate == plate &&
                !x.is_deleted);
    }

    public async Task<List<registered_vehicle>>
        GetAllCompleteAsync()
    {
        return await _context.registered_vehicles
            .Include(x => x.vehicle_monthly_plan)
            .Include(x => x.vehicle_type)
            .Include(x => x.monthly_vehicle_schedules)
            .Where(x => !x.is_deleted)
            .OrderByDescending(x => x.id)
            .ToListAsync();
    }

    public async Task<registered_vehicle?>
        GetCompleteByIdAsync(int id)
    {
        return await _context.registered_vehicles
            .Include(x => x.vehicle_monthly_plan)
            .Include(x => x.vehicle_type)
            .Include(x => x.monthly_vehicle_schedules)
            .FirstOrDefaultAsync(x =>
                x.id == id &&
                !x.is_deleted);
    }

    public async Task AddMonthlyPlanAsync(
        vehicle_monthly_plan plan)
    {
        await _context.vehicle_monthly_plans
            .AddAsync(plan);
    }

    public async Task AddSchedulesAsync(
        List<monthly_vehicle_schedule> schedules)
    {
        await _context.monthly_vehicle_schedules
            .AddRangeAsync(schedules);
    }

    public async Task RemoveSchedulesAsync(
        List<monthly_vehicle_schedule> schedules)
    {
        foreach (var item in schedules)
        {
            item.is_deleted = true;

            item.deleted_at = DateTime.Now;

            item.updated_at = DateTime.Now;
        }

        _context.monthly_vehicle_schedules
            .UpdateRange(schedules);

        await Task.CompletedTask;
    }

    public async Task SoftDeleteAsync(
        registered_vehicle entity,
        int deletedBy)
    {
        entity.is_deleted = true;

        entity.deleted_at = DateTime.Now;

        entity.deleted_by = deletedBy;

        entity.updated_at = DateTime.Now;

        entity.updated_by = deletedBy;

        entity.is_active = false;

        // =========================
        // PLAN
        // =========================

        if (entity.vehicle_monthly_plan != null)
        {
            entity.vehicle_monthly_plan.is_deleted = true;

            entity.vehicle_monthly_plan.deleted_at =
                DateTime.Now;

            entity.vehicle_monthly_plan.deleted_by =
                deletedBy;

            entity.vehicle_monthly_plan.updated_at =
                DateTime.Now;

            entity.vehicle_monthly_plan.updated_by =
                deletedBy;

            entity.vehicle_monthly_plan.is_active =
                false;

            entity.vehicle_monthly_plan.status =
                "cancelled";
        }

        // =========================
        // HORARIOS
        // =========================

        if (entity.monthly_vehicle_schedules != null)
        {
            foreach (var schedule
                in entity.monthly_vehicle_schedules)
            {
                schedule.is_deleted = true;

                schedule.deleted_at = DateTime.Now;

                schedule.deleted_by = deletedBy;

                schedule.updated_at = DateTime.Now;

                schedule.updated_by = deletedBy;

                schedule.is_active = false;
            }
        }

        _context.registered_vehicles
            .Update(entity);

        await Task.CompletedTask;
    }
}