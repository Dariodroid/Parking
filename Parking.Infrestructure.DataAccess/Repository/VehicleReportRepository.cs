using Microsoft.EntityFrameworkCore;
using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.Infrastructure.DataAccess;

namespace Parking.Infrastructure.DataAccess.Repository;

public class VehicleReportRepository
    : IVehicleReportRepository
{
    private readonly parking_dbContext _context;

    public VehicleReportRepository(
        parking_dbContext context)
    {
        _context = context;
    }

    public async Task<List<VehicleReportDto>>
        GetReportAsync(
            VehicleReportFilterDto filter)
    {
        var query =
            _context.registered_vehicles
            .Where(x => !x.is_deleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Plate))
        {
            query =
                query.Where(x =>
                    x.plate.Contains(filter.Plate));
        }

        if (!string.IsNullOrWhiteSpace(filter.OwnerName))
        {
            query =
                query.Where(x =>
                    x.owner_name.Contains(filter.OwnerName));
        }

        if (filter.IncludeMonthly &&
            !filter.IncludeOccasional)
        {
            query =
                query.Where(x =>
                    x.vehicle_monthly_plan != null);
        }

        if (!filter.IncludeMonthly &&
            filter.IncludeOccasional)
        {
            query =
                query.Where(x =>
                    x.vehicle_monthly_plan == null);
        }

        return await query
            .Select(x =>
                new VehicleReportDto
                {
                    Plate = x.plate,

                    OwnerName =
                        x.owner_name,

                    VehicleType =
                        x.vehicle_type.name,

                    Category =
                        x.vehicle_monthly_plan != null
                            ? "Mensual"
                            : "Ocasional",

                    PlanStatus =
                        x.vehicle_monthly_plan != null
                            ? x.vehicle_monthly_plan.status
                            : "-",

                    MonthlyFee =
                        x.vehicle_monthly_plan != null
                            ? x.vehicle_monthly_plan.monthly_fee
                            : 0,

                    PlanStartDate =
                        x.vehicle_monthly_plan != null
                            ? x.vehicle_monthly_plan.start_date
                            : null,

                    PlanEndDate =
                        x.vehicle_monthly_plan != null
                            ? x.vehicle_monthly_plan.end_date
                            : null,

                    TotalEntries =
                        x.parking_sessions.Count(),

                    LastEntryDate =
                        x.parking_sessions
                            .OrderByDescending(s =>
                                s.entry_time)
                            .Select(s =>
                                (DateTime?)s.entry_time)
                            .FirstOrDefault()
                })
            .OrderBy(x => x.Plate)
            .ToListAsync();
    }
}