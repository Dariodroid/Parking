namespace Parking.Domain.Model.Policies;

/// <summary>Decide qué sesiones y cuotas requieren atención en el centro de control.</summary>
public static class ControlIncidentPolicy
{
    /// <summary>Primer momento que se revisa para detectar diferencias de cobro.</summary>
    public static DateTime PaymentReviewSince(DateTime now) => now.AddDays(-90);

    /// <summary>Decide si una sesión requiere atención por antigüedad o diferencia de cobro.</summary>
    public static ControlIncidentFinding? EvaluateSession(string status, DateTime entryTime,
        DateTime? exitTime, bool hasRegisteredVehicle, decimal? amountDue, decimal paidTotal, DateTime now)
    {
        if (status == "active" && entryTime < now.AddHours(-48) && !hasRegisteredVehicle)
            return new(ControlIncidentKind.ProlongedSession, entryTime, 0m);

        if (status == "paid" && exitTime >= PaymentReviewSince(now) && amountDue > 0m)
        {
            decimal difference = amountDue.Value - paidTotal;
            if (Math.Abs(difference) >= 0.01m)
                return new(ControlIncidentKind.PaymentDifference, exitTime ?? entryTime, difference);
        }
        return null;
    }

    /// <summary>Decide si el contrato vencido tiene una cuota pendiente.</summary>
    public static ControlIncidentFinding? EvaluateOverdueFee(DateTime endDate, DateTime paymentDate,
        decimal monthlyFee, DateTime today) =>
        endDate.Date < today.Date && paymentDate.Date <= endDate.Date && monthlyFee > 0m
            ? new(ControlIncidentKind.OverdueFee, endDate, monthlyFee)
            : null;

    /// <summary>Valida y normaliza la explicación del operador que revisa una incidencia.</summary>
    public static string ValidateReview(string key, string reason, int operatorId)
    {
        reason = reason.Trim();
        if (operatorId <= 0 || key.Length > 100 || reason.Length is < 8 or > 500)
            throw new ArgumentException("Indique un motivo de 8 a 500 caracteres.");
        return reason;
    }
}
