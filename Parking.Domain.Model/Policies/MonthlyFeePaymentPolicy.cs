namespace Parking.Domain.Model.Policies;

/// <summary>Decide si una cuota vencida puede cobrarse sin renovar el contrato.</summary>
public static class MonthlyFeePaymentPolicy
{
    public static void RequirePendingPeriod(DateTime collectedAt, DateTime endDate, DateTime lastPaid)
    {
        if (collectedAt.Date <= endDate.Date || lastPaid.Date > endDate.Date)
            throw new InvalidOperationException("Este contrato no tiene una cuota vencida pendiente.");
    }

    public static void RequirePositiveAmount(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidOperationException("La cuota mensual debe ser mayor que cero.");
    }
}
