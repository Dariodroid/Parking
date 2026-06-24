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
        GetReportAsync(VehicleReportFilterDto filter)
    {
        var result =
            new List<VehicleReportDto>();

        // ==================================================
        // CONTROL DE TIPOS DE BÚSQUEDA (CORREGIDO)
        // ==================================================

        var searchMonthly =
            filter.IncludeMonthly;

        var searchOccasional =
            filter.IncludeOccasional;

        if (!searchMonthly &&
            !searchOccasional)
        {
            if (!string.IsNullOrWhiteSpace(filter.OwnerName))
            {
                searchMonthly = true;
                searchOccasional = false;
            }
            else
            {
                searchMonthly = true;
                searchOccasional = true;
            }
        }

        // ==================================================
        // MENSUALES
        // ==================================================

        if (searchMonthly)
        {
            var query =
                _context.registered_vehicles
                .Where(x => !x.is_deleted)
                .AsQueryable();

            // -----------------------------
            // FILTRO PLACA
            // -----------------------------

            if (!string.IsNullOrWhiteSpace(filter.Plate))
            {
                var plate =
                    filter.Plate.Trim();

                query =
                    query.Where(x =>
                        x.plate.Contains(plate));
            }

            // -----------------------------
            // FILTRO PROPIETARIO (CORREGIDO)
            // -----------------------------

            if (!string.IsNullOrWhiteSpace(filter.OwnerName))
            {
                var owner =
                    filter.OwnerName.Trim();

                query =
                    query.Where(x =>
                        x.owner_name.Contains(owner));
            }

            // -----------------------------
            // FILTRO TIPO VEHÍCULO
            // -----------------------------

            if (filter.VehicleTypeId.HasValue)
            {
                query =
                    query.Where(x =>
                        x.vehicle_type_id ==
                        filter.VehicleTypeId.Value);
            }

            // -----------------------------
            // FILTRO FECHAS
            // -----------------------------

            if (filter.FromDate.HasValue ||
                filter.ToDate.HasValue)
            {
                var fromDate =
                    filter.FromDate ??
                    DateTime.MinValue;

                var toDate =
                    filter.ToDate?.Date.AddDays(1) ??
                    DateTime.MaxValue;

                query =
                    query.Where(x =>
                        !x.parking_sessions.Any()
                        ||
                        x.parking_sessions.Any(s =>
                            s.entry_time >= fromDate &&
                            s.entry_time < toDate));
            }

            var monthly =
                await query
                .Select(x =>
                    new VehicleReportDto
                    {
                        Plate = x.plate,

                        OwnerName =
                            x.owner_name,

                        VehicleType =
                            x.vehicle_type.name,

                        Category =
                            "Mensual",

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
                            .FirstOrDefault(),

                        LastExitDate =
                            x.parking_sessions
                            .OrderByDescending(s =>
                                s.exit_time)
                            .Select(s =>
                                s.exit_time)
                            .FirstOrDefault(),

                        TotalMinutesParked =
                            x.parking_sessions
                            .Sum(s =>
                                s.duration_minutes ?? 0),

                        TotalCollected =
                            x.parking_sessions
                            .SelectMany(s =>
                                s.payments)
                            .Sum(p =>
                                (decimal?)p.amount_paid)
                            ?? 0,

                        CurrentStatus =
                            x.parking_sessions
                            .Any(s =>
                                s.exit_time == null)
                                ? "Dentro"
                                : "Fuera"
                    })
                .ToListAsync();

            result.AddRange(monthly);
        }

        // ==================================================
        // OCASIONALES
        // ==================================================

        if (searchOccasional)
        {
            var query =
                _context.parking_sessions
                .Where(x =>
                    !x.is_deleted &&
                    x.registered_vehicle_id == null)
                .AsQueryable();

            // -----------------------------
            // FILTRO PLACA
            // -----------------------------

            if (!string.IsNullOrWhiteSpace(filter.Plate))
            {
                var plate =
                    filter.Plate.Trim();

                query =
                    query.Where(x =>
                        x.plate.Contains(plate));
            }

            // -----------------------------
            // FILTRO FECHAS
            // -----------------------------

            if (filter.FromDate.HasValue)
            {
                query =
                    query.Where(x =>
                        x.entry_time >=
                        filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                var endDate =
                    filter.ToDate.Value
                    .Date
                    .AddDays(1);

                query =
                    query.Where(x =>
                        x.entry_time < endDate);
            }

            // -----------------------------
            // FILTRO TIPO VEHÍCULO
            // -----------------------------

            if (filter.VehicleTypeId.HasValue)
            {
                query =
                    query.Where(x =>
                        x.vehicle_type_id ==
                        filter.VehicleTypeId.Value);
            }

            var occasional =
                await query
                .GroupBy(x =>
                    new
                    {
                        x.plate,
                        VehicleType =
                            x.vehicle_type.name
                    })
                .Select(g =>
                    new VehicleReportDto
                    {
                        Plate =
                            g.Key.plate,

                        OwnerName =
                            "Ocasional",

                        VehicleType =
                            g.Key.VehicleType,

                        Category =
                            "Ocasional",

                        PlanStatus =
                            "-",

                        MonthlyFee =
                            0,

                        TotalEntries =
                            g.Count(),

                        LastEntryDate =
                            g.Max(x =>
                                x.entry_time),

                        LastExitDate =
                            g.Max(x =>
                                x.exit_time),

                        TotalMinutesParked =
                            g.Sum(x =>
                                x.duration_minutes ?? 0),

                        TotalCollected =
                            g.SelectMany(x =>
                                x.payments)
                            .Sum(x =>
                                (decimal?)x.amount_paid)
                            ?? 0,

                        CurrentStatus =
                            g.Any(x =>
                                x.exit_time == null)
                                ? "Dentro"
                                : "Fuera"
                    })
                .ToListAsync();

            result.AddRange(occasional);
        }

        // ==================================================
        // FILTRO DENTRO / FUERA
        // ==================================================

        if (filter.IncludeInside &&
            !filter.IncludeOutside)
        {
            result =
                result
                .Where(x =>
                    x.CurrentStatus == "Dentro")
                .ToList();
        }
        else if (!filter.IncludeInside &&
                 filter.IncludeOutside)
        {
            result =
                result
                .Where(x =>
                    x.CurrentStatus == "Fuera")
                .ToList();
        }

        // ==================================================
        // ORDENAMIENTO FINAL
        // ==================================================

        return result
            .OrderBy(x => x.Plate)
            .ToList();
    }
}