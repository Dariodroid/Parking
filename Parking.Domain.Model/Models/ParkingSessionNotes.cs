namespace Parking.Domain.Model.Models;

/// <summary>Marcas persistidas que identifican cómo se autorizó una estancia.</summary>
public static class ParkingSessionNotes
{
    /// <summary>La entrada tuvo cobertura del plan mensual y no genera cobro de estacionamiento.</summary>
    public const string MonthlyPlan = "PLAN_MENSUAL";

    /// <summary>Prefijo del motivo por el que un cliente registrado ingresó con tarifa ocasional.</summary>
    public const string OccasionalReasonPrefix = "ACCESO_OCASIONAL:";
}
