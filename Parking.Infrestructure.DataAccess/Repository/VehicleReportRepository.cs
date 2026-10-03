using Microsoft.EntityFrameworkCore;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess.Repository;

/// <summary>Consulta fichas, estancias y pagos para el informe de vehículos.</summary>
public class VehicleReportRepository : IVehicleReportRepository
{
    private readonly parking_dbContext _context;

    /// <summary>Recibe el contexto de la base de datos del parqueadero.</summary>
    public VehicleReportRepository(parking_dbContext context) => _context = context;

    /// <summary>Reúne las filas mensuales y ocasionales respetando todos los filtros elegidos.</summary>
    public async Task<List<VehicleReportDto>> GetReportAsync(VehicleReportFilterDto filter)
    {
        bool monthly = filter.IncludeMonthly;
        bool occasional = filter.IncludeOccasional;
        if (!monthly && !occasional)
        {
            monthly = true;
            occasional = string.IsNullOrWhiteSpace(filter.OwnerName);
        }

        var rows = new List<VehicleReportDto>();
        if (monthly) rows.AddRange(await GetMonthlyRowsAsync(filter));
        if (occasional) rows.AddRange(await GetOccasionalRowsAsync(filter));

        if (filter.IncludeInside && !filter.IncludeOutside)
            rows = rows.Where(x => x.CurrentStatus == "Dentro").ToList();
        else if (!filter.IncludeInside && filter.IncludeOutside)
            rows = rows.Where(x => x.CurrentStatus == "Fuera").ToList();
        return rows.OrderBy(x => x.Plate).ToList();
    }

    /// <summary>Consulta clientes mensuales y suma solo sus estancias de la placa y período actuales.</summary>
    private async Task<List<VehicleReportDto>> GetMonthlyRowsAsync(VehicleReportFilterDto filter)
    {
        var query = _context.registered_vehicles.Where(x => !x.is_deleted).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Plate))
        {
            string plate = filter.Plate.Trim();
            query = query.Where(x => x.plate.Contains(plate));
        }
        if (!string.IsNullOrWhiteSpace(filter.OwnerName))
        {
            string owner = filter.OwnerName.Trim();
            query = query.Where(x => x.owner_name.Contains(owner));
        }
        if (filter.VehicleTypeId.HasValue)
            query = query.Where(x => x.vehicle_type_id == filter.VehicleTypeId.Value);

        DateTime from = filter.FromDate?.Date ?? DateTime.MinValue;
        DateTime until = filter.ToDate?.Date.AddDays(1) ?? DateTime.MaxValue;
        if (filter.FromDate.HasValue || filter.ToDate.HasValue)
            query = query.Where(x => x.parking_sessions.Any(s => !s.is_deleted &&
                s.plate == x.plate && s.entry_time >= from && s.entry_time < until));

        var vehicles = await query.AsNoTracking()
            .Include(x => x.vehicle_type)
            .Include(x => x.vehicle_monthly_plan)
            .Include(x => x.parking_sessions.Where(s =>
                !s.is_deleted && s.entry_time >= from && s.entry_time < until))
            .ThenInclude(s => s.payments.Where(p => !p.is_deleted))
            .AsSplitQuery().ToListAsync();

        var rows = new List<VehicleReportDto>(vehicles.Count);
        foreach (var vehicle in vehicles)
        {
            var sessions = vehicle.parking_sessions.Where(s =>
                string.Equals(s.plate, vehicle.plate, StringComparison.OrdinalIgnoreCase)).ToList();
            var row = new VehicleReportDto
            {
                Plate = vehicle.plate,
                OwnerName = vehicle.owner_name,
                VehicleType = vehicle.vehicle_type.name,
                Category = "Mensual",
                AccessSummary = GetAccessSummary(sessions),
                PlanStatus = vehicle.vehicle_monthly_plan?.status ?? "Sin Plan",
                VehicleIsActive = vehicle.is_active,
                PlanIsActive = vehicle.vehicle_monthly_plan?.is_active ?? false,
                MonthlyFee = vehicle.vehicle_monthly_plan?.monthly_fee ?? 0,
                PlanStartDate = vehicle.vehicle_monthly_plan?.start_date,
                PlanEndDate = vehicle.vehicle_monthly_plan?.end_date
            };
            FillActivity(row, sessions);
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>Consulta estancias ocasionales y agrupa la actividad por placa y tipo de vehículo.</summary>
    private async Task<List<VehicleReportDto>> GetOccasionalRowsAsync(VehicleReportFilterDto filter)
    {
        var query = _context.parking_sessions
            .Where(x => !x.is_deleted && x.registered_vehicle_id == null).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Plate))
        {
            string plate = filter.Plate.Trim();
            query = query.Where(x => x.plate.Contains(plate));
        }
        if (filter.FromDate.HasValue)
            query = query.Where(x => x.entry_time >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
        {
            DateTime endDate = filter.ToDate.Value.Date.AddDays(1);
            query = query.Where(x => x.entry_time < endDate);
        }
        if (filter.VehicleTypeId.HasValue)
            query = query.Where(x => x.vehicle_type_id == filter.VehicleTypeId.Value);

        var sessions = await query.AsNoTracking()
            .Include(s => s.vehicle_type)
            .Include(s => s.payments.Where(p => !p.is_deleted))
            .AsSplitQuery().ToListAsync();

        var rows = new List<VehicleReportDto>();
        foreach (var group in sessions.GroupBy(s => new { s.plate, s.vehicle_type.name }))
        {
            var row = new VehicleReportDto
            {
                Plate = group.Key.plate,
                OwnerName = "Ocasional",
                VehicleType = group.Key.name,
                Category = "Ocasional",
                PlanStatus = "-"
            };
            FillActivity(row, group);
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>Describe la modalidad registrada en las estancias de un cliente mensual.</summary>
    private static string GetAccessSummary(IReadOnlyList<parking_session> sessions)
    {
        if (sessions.Count == 0) return "Sin ingresos";
        int monthlyCount = sessions.Count(s => s.notes == ParkingSessionNotes.MonthlyPlan);
        if (monthlyCount == sessions.Count) return "Acceso mensual";
        if (monthlyCount > 0) return "Accesos mixtos";

        var reasons = sessions.Select(s => s.notes)
            .Where(note => note?.StartsWith(ParkingSessionNotes.OccasionalReasonPrefix,
                StringComparison.Ordinal) == true)
            .Select(note => note![ParkingSessionNotes.OccasionalReasonPrefix.Length..])
            .Distinct(StringComparer.Ordinal).ToList();
        if (reasons.Count != 1 || sessions.Any(s => s.notes is null ||
            !s.notes.StartsWith(ParkingSessionNotes.OccasionalReasonPrefix, StringComparison.Ordinal)))
            return "Tarifa ocasional";

        return reasons[0] switch
        {
            "OutsideSchedule" => "Ocasional: fuera de horario",
            "Expired" => "Ocasional: contrato vencido",
            "Inactive" => "Ocasional: plan inactivo",
            "NotStarted" => "Ocasional: plan sin iniciar",
            _ => "Tarifa ocasional"
        };
    }

    /// <summary>Suma la actividad del período y toma entrada y salida de la misma última estancia.</summary>
    private static void FillActivity(VehicleReportDto row, IEnumerable<parking_session> sessions)
    {
        var activity = sessions.ToList();
        var latest = activity.MaxBy(s => s.entry_time);
        row.TotalEntries = activity.Count;
        row.TotalMinutesParked = activity.Sum(s => s.duration_minutes ?? 0);
        row.TotalCollected = activity.SelectMany(s => s.payments).Sum(p => p.amount_paid);
        row.LastEntryDate = latest?.entry_time;
        row.LastExitDate = latest?.exit_time;
        row.CurrentStatus = activity.Any(s => s.exit_time == null) ? "Dentro" : "Fuera";
    }
}
