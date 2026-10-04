namespace Parking.Domain.Model.Policies;

/// <summary>Aplica la regla de hora iniciada o el acceso mensual gratuito de la sesión.</summary>
public static class ParkingChargePolicy
{
    /// <summary>Calcula los minutos reales, los facturables y el importe sin redondear dinero.</summary>
    public static ParkingCharge Calculate(TimeSpan duration, bool monthlyAccess, decimal hourlyRate)
    {
        int durationMinutes = (int)duration.TotalMinutes;
        if (monthlyAccess)
            return new ParkingCharge(durationMinutes, 0, 0m);

        decimal hours = (decimal)Math.Ceiling(duration.TotalHours);
        if (hours < 1) hours = 1;
        return new ParkingCharge(durationMinutes, (int)(hours * 60), hours * hourlyRate);
    }
}
