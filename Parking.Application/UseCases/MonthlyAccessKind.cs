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
