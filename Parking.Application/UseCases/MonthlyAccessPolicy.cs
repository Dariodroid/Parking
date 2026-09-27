using Parking.Domain.Model.Models;

namespace Parking.Application.UseCases;

/// <summary>Motivo por el que una entrada recibe acceso mensual o tarifa ocasional.</summary>
public enum MonthlyAccessKind
{
    /// <summary>La placa no tiene un contrato mensual utilizable.</summary>
    Occasional,
    /// <summary>Contrato vigente y entrada dentro del horario permitido.</summary>
    Monthly,
    /// <summary>Contrato vigente, pero entrada fuera del horario configurado.</summary>
    OutsideSchedule,
    /// <summary>La fecha final del contrato ya pasó.</summary>
    Expired,
    /// <summary>Vehículo o plan marcado como inactivo o cancelado.</summary>
    Inactive,
    /// <summary>La fecha inicial del contrato todavía no llega.</summary>
    NotStarted
}

/// <summary>Resultado de evaluar el contrato antes de abrir una sesión de estacionamiento.</summary>
/// <param name="Kind">Clasificación que determina si se genera ticket y se cobra estancia.</param>
/// <param name="PendingFee">Cuota mensual vencida pendiente según la fecha de pago registrada.</param>
public sealed record MonthlyAccessDecision(MonthlyAccessKind Kind, decimal PendingFee)
{
    /// <summary>Indica que la sesión se abrirá como mensualizada, sin ticket ni cobro de estancia.</summary>
    public bool IsMonthly => Kind == MonthlyAccessKind.Monthly;
}

/// <summary>Aplica las fechas, el estado y el horario semanal del plan mensual al momento de entrada.</summary>
public static class MonthlyAccessPolicy
{
    /// <summary>Marca persistida en la sesión para conservar la modalidad mensual hasta la salida.</summary>
    public const string MonthlySessionNote = "PLAN_MENSUAL";

    /// <summary>
    /// Clasifica el ingreso usando el contrato y el horario del día correspondiente.
    /// La marca 24H omite las horas únicamente para el día activo donde fue configurada.
    /// </summary>
    /// <param name="vehicle">Vehículo registrado con plan y horarios cargados; puede ser nulo si la placa es ocasional.</param>
    /// <param name="entryTime">Fecha y hora locales exactas en que se registra la entrada.</param>
    /// <returns>Modalidad del ingreso y una cuota pendiente, si el plan ya venció y no consta pagada.</returns>
    public static MonthlyAccessDecision Evaluate(registered_vehicle? vehicle, DateTime entryTime)
    {
        // Una placa no registrada o un registro eliminado no tiene beneficios mensuales.
        if (vehicle == null || vehicle.is_deleted)
            return new(MonthlyAccessKind.Occasional, 0);

        // El vehículo puede existir sin un plan utilizable; también entra como ocasional.
        var plan = vehicle.vehicle_monthly_plan;
        if (plan == null || plan.is_deleted)
            return new(MonthlyAccessKind.Occasional, 0);

        // El importe pendiente representa una cuota de renovación. La fecha de
        // pago posterior al vencimiento permite saldarla sin activar el contrato.
        // Se compara por día para que la fecha final incluya todas sus horas.
        decimal pendingFee = entryTime.Date > plan.end_date.Date
            && plan.payment_date.Date <= plan.end_date.Date
            ? plan.monthly_fee : 0;

        // Un estado inactivo impide el acceso mensual aunque las fechas coincidan.
        if (!vehicle.is_active || !plan.is_active || plan.status == "cancelled")
            return new(MonthlyAccessKind.Inactive, pendingFee);
        // El plan todavía no cubre una entrada anterior a su fecha inicial.
        if (entryTime.Date < plan.start_date.Date)
            return new(MonthlyAccessKind.NotStarted, 0);
        // Tras la fecha final se emite ticket ocasional y se informa la cuota pendiente.
        if (entryTime.Date > plan.end_date.Date)
            return new(MonthlyAccessKind.Expired, pendingFee);

        // DayOfWeek usa domingo=0 ... sábado=6, igual que day_of_week en la base.
        TimeOnly time = TimeOnly.FromDateTime(entryTime);
        int day = (int)entryTime.DayOfWeek;
        // Solo se consideran horarios activos del día de entrada. En 24H se
        // ignoran start_time/end_time; en los demás se compara el rango horario.
        // Si el rango cruza medianoche, su primera parte pertenece al día actual.
        bool allowed = vehicle.monthly_vehicle_schedules.Any(s =>
            !s.is_deleted && s.is_active && s.day_of_week == day
            && (s.is_full_day || (s.start_time <= s.end_time
                && time >= s.start_time && time < s.end_time)
                || (s.start_time > s.end_time && time >= s.start_time)));

        // La madrugada también puede pertenecer a un turno iniciado el día anterior.
        int previousDay = (day + 6) % 7;
        allowed |= vehicle.monthly_vehicle_schedules.Any(s =>
            !s.is_deleted && s.is_active && s.day_of_week == previousDay
            && !s.is_full_day && s.start_time > s.end_time && time < s.end_time);

        // El resultado queda fijado al crear la sesión; no se recalcula al salir.
        return new(allowed ? MonthlyAccessKind.Monthly : MonthlyAccessKind.OutsideSchedule, 0);
    }
}
