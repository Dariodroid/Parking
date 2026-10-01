using Microsoft.EntityFrameworkCore;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Infrastructure.DataAccess;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess.Repository;

public class VehicleReportRepository
    : IVehicleReportRepository
{
    private readonly parking_dbContext _context;

    /// <summary>Prepara el acceso a las fichas, estancias y pagos del informe.</summary>
    /// <param name="context">Contexto de la base de datos del parqueadero.</param>
    public VehicleReportRepository(
        parking_dbContext context)
    {
        _context = context;
    }

    /// <summary>Consulta vehículos y calcula su actividad según placa, período y demás criterios seleccionados.</summary>
    /// <param name="filter">Criterios de búsqueda que limitan las filas y los movimientos contabilizados.</param>
    /// <returns>Filas del informe ordenadas por placa.</returns>
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
                    filter.FromDate?.Date ??
                    DateTime.MinValue;

                var toDate =
                    filter.ToDate?.Date.AddDays(1) ??
                    DateTime.MaxValue;

                query =
                    query.Where(x =>
                        x.parking_sessions.Any(s =>
                            !s.is_deleted &&
                            s.plate == x.plate &&
                            s.entry_time >= fromDate &&
                            s.entry_time < toDate));
            }

            // La ficha puede conservar sesiones de otra placa anterior; la actividad se filtra por la placa actual.
            var from = filter.FromDate?.Date ?? DateTime.MinValue;
            var until = filter.ToDate?.Date.AddDays(1) ?? DateTime.MaxValue;
            var monthlyVehicles = await query.AsNoTracking()
                .Include(x => x.vehicle_type)
                .Include(x => x.vehicle_monthly_plan)
                .Include(x => x.parking_sessions.Where(s =>
                    !s.is_deleted && s.entry_time >= from && s.entry_time < until))
                .ThenInclude(s => s.payments.Where(p => !p.is_deleted))
                .AsSplitQuery()
                .ToListAsync();

            foreach (var vehicle in monthlyVehicles)
            {
                // Los importes pertenecen únicamente a pagos de estancias de esta placa y este período.
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
                result.Add(row);
            }
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

            // La misma regla de cobros y movimientos se aplica a cada placa ocasional.
            var occasionalSessions = await query.AsNoTracking()
                .Include(s => s.vehicle_type)
                .Include(s => s.payments.Where(p => !p.is_deleted))
                .AsSplitQuery()
                .ToListAsync();

            foreach (var group in occasionalSessions.GroupBy(s => new { s.plate, s.vehicle_type.name }))
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
                result.Add(row);
            }
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

    /// <summary>Describe la modalidad registrada en las estancias de un cliente mensual.</summary>
    /// <param name="sessions">Estancias ya filtradas por placa y período.</param>
    /// <returns>Modalidad del período y, cuando quedó guardado, motivo de la tarifa ocasional.</returns>
    private static string GetAccessSummary(IReadOnlyList<parking_session> sessions)
    {
        // Las estancias anteriores al registro del motivo siguen mostrándose sin inventar una causa.
        if (sessions.Count == 0) return "Sin ingresos";
        var monthlyCount = sessions.Count(s => s.notes == ParkingSessionNotes.MonthlyPlan);
        if (monthlyCount == sessions.Count) return "Acceso mensual";
        if (monthlyCount > 0) return "Accesos mixtos";

        // Solo se presenta un motivo concreto si todas las estancias comparten uno registrado.
        var reasons = sessions.Select(s => s.notes)
            .Where(note => note?.StartsWith(ParkingSessionNotes.OccasionalReasonPrefix,
                StringComparison.Ordinal) == true)
            .Select(note => note![ParkingSessionNotes.OccasionalReasonPrefix.Length..])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (reasons.Count != 1 || sessions.Any(s => s.notes is null ||
            !s.notes.StartsWith(ParkingSessionNotes.OccasionalReasonPrefix, StringComparison.Ordinal)))
            return "Tarifa ocasional";

        // Los códigos persistidos se traducen sin perder la razón original en la base de datos.
        return reasons[0] switch
        {
            "OutsideSchedule" => "Ocasional: fuera de horario",
            "Expired" => "Ocasional: contrato vencido",
            "Inactive" => "Ocasional: plan inactivo",
            "NotStarted" => "Ocasional: plan sin iniciar",
            _ => "Tarifa ocasional"
        };
    }

    /// <summary>Calcula ingresos, tiempo y pagos de las estancias ya filtradas y toma ambos movimientos de la última estancia.</summary>
    /// <param name="row">Fila del informe que recibirá los totales y el estado.</param>
    /// <param name="sessions">Estancias de una sola placa dentro del período solicitado.</param>
    private static void FillActivity(VehicleReportDto row, IEnumerable<parking_session> sessions)
    {
        // Se materializa una vez para que totales y último movimiento usen el mismo conjunto.
        var activity = sessions.ToList();
        var latest = activity.MaxBy(s => s.entry_time);

        // Los minutos son la suma de las duraciones reales persistidas, no los minutos facturables.
        row.TotalEntries = activity.Count;
        row.TotalMinutesParked = activity.Sum(s => s.duration_minutes ?? 0);
        row.TotalCollected = activity.SelectMany(s => s.payments).Sum(p => p.amount_paid);

        // Entrada y salida corresponden a una misma estancia, incluso si otra terminó después.
        row.LastEntryDate = latest?.entry_time;
        row.LastExitDate = latest?.exit_time;
        row.CurrentStatus = activity.Any(s => s.exit_time == null) ? "Dentro" : "Fuera";
    }
}
