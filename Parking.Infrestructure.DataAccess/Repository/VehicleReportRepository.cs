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
        var result =
            new List<VehicleReportDto>();

        // =====================================
        // VEHICULOS MENSUALES / REGISTRADOS
        // =====================================

        if (filter.IncludeMonthly)
        {
            var monthlyQuery =
                _context.registered_vehicles
                .Where(x => !x.is_deleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Plate))
            {
                monthlyQuery =
                    monthlyQuery.Where(x =>
                        x.plate.Contains(filter.Plate));
            }

            if (!string.IsNullOrWhiteSpace(filter.OwnerName))
            {
                monthlyQuery =
                    monthlyQuery.Where(x =>
                        x.owner_name.Contains(filter.OwnerName));
            }

            var monthlyData =
                await monthlyQuery
                .Select(x =>
                    new VehicleReportDto
                    {
                        Plate = x.plate,

                        OwnerName = x.owner_name,

                        VehicleType =
                            x.vehicle_type.name,

                        Category = "Mensual",

                        PlanStatus =
                            x.vehicle_monthly_plan != null
                                ? x.vehicle_monthly_plan.status
                                : "Sin Plan",

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
                .ToListAsync();

            result.AddRange(monthlyData);
        }

        // =====================================
        // VEHICULOS OCASIONALES
        // =====================================

        if (filter.IncludeOccasional)
        {
            var occasionalQuery =
                _context.parking_sessions
                .Where(x =>
                    !x.is_deleted &&
                    x.registered_vehicle_id == null)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Plate))
            {
                occasionalQuery =
                    occasionalQuery.Where(x =>
                        x.plate.Contains(filter.Plate));
            }

            if (filter.FromDate.HasValue)
            {
                occasionalQuery =
                    occasionalQuery.Where(x =>
                        x.entry_time >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                var endDate =
                    filter.ToDate.Value.Date.AddDays(1);

                occasionalQuery =
                    occasionalQuery.Where(x =>
                        x.entry_time < endDate);
            }

            var occasionalData =
                await occasionalQuery
                .GroupBy(x => new
                {
                    x.plate,
                    VehicleType =
                        x.vehicle_type.name
                })
                .Select(g =>
                    new VehicleReportDto
                    {
                        Plate = g.Key.plate,

                        OwnerName = "Ocasional",

                        VehicleType =
                            g.Key.VehicleType,

                        Category = "Ocasional",

                        PlanStatus = "-",

                        MonthlyFee = 0,

                        TotalEntries =
                            g.Count(),

                        LastEntryDate =
                            g.Max(x =>
                                x.entry_time)
                    })
                .ToListAsync();

            result.AddRange(occasionalData);
        }

        return result
            .OrderBy(x => x.Plate)
            .ToList();
    }
}