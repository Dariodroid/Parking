namespace Parking.Domain.Model.Policies;

/// <summary>Duración real y cobro resultante de una estancia.</summary>
public sealed class ParkingCharge
{
    public int DurationMinutes { get; }
    public int ChargeableMinutes { get; }
    public decimal AmountDue { get; }

    /// <summary>Conserva los tres resultados de un único cálculo de salida.</summary>
    public ParkingCharge(int durationMinutes, int chargeableMinutes, decimal amountDue)
    {
        DurationMinutes = durationMinutes;
        ChargeableMinutes = chargeableMinutes;
        AmountDue = amountDue;
    }
}
