namespace Parking.Domain.Model.Policies;

/// <summary>Duración real y cobro resultante de una estancia.</summary>
public sealed record ParkingCharge(int DurationMinutes, int ChargeableMinutes, decimal AmountDue);

/// <summary>Aplica la regla de hora iniciada o el acceso mensual gratuito de la sesión.</summary>
public static class ParkingChargePolicy
{
    /// <summary>Calcula los minutos reales, los facturables y el importe sin redondear dinero.</summary>
    public static ParkingCharge Calculate(TimeSpan duration, bool monthlyAccess, decimal hourlyRate)
    {
        decimal hours = Math.Max(1m, (decimal)Math.Ceiling(duration.TotalHours));
        return new ParkingCharge((int)duration.TotalMinutes,
            monthlyAccess ? 0 : (int)(hours * 60),
            monthlyAccess ? 0m : hours * hourlyRate);
    }
}
