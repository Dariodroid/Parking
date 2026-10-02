namespace Parking.Domain.Model.Policies;

/// <summary>Tipos de incidencias que resultan de las reglas del parqueadero.</summary>
public enum ControlIncidentKind { ProlongedSession, PaymentDifference, OverdueFee }

/// <summary>Decisión del dominio sobre una sesión o cuota, sin texto ni formato de UI.</summary>
public sealed record ControlIncidentFinding(ControlIncidentKind Kind, DateTime OccurredAt, decimal Difference);

/// <summary>Pagos ya agrupados por medio, independientes de EF y de la base de datos.</summary>
public sealed record ShiftPaymentGroup(string? Method, decimal Amount, int Count);

/// <summary>Totales del turno calculados por las reglas del dominio.</summary>
public sealed record ShiftTotals(decimal Cash, decimal Transfer, decimal Card, decimal Other, int PaymentCount);

/// <summary>Resultado de comparar el efectivo entregado con el esperado.</summary>
public sealed record ShiftCloseAssessment(decimal Difference, string Note);

/// <summary>Reglas de incidencias, revisión y conciliación de caja del parqueadero.</summary>
public static class OperationsControlPolicy
{
    /// <summary>Primer momento que se revisa para detectar diferencias de cobro.</summary>
    public static DateTime PaymentReviewSince(DateTime now) => now.AddDays(-90);

    /// <summary>El turno empieza en el último cierre del día, o a medianoche si no existe.</summary>
    public static DateTime DetermineShiftStart(DateTime lastClosed, DateTime now) =>
        lastClosed > now.Date ? lastClosed : now.Date;

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

    /// <summary>Clasifica los pagos del turno en efectivo, transferencia, tarjeta y otros.</summary>
    public static ShiftTotals CalculateShift(IEnumerable<ShiftPaymentGroup> payments)
    {
        decimal cash = 0, transfer = 0, card = 0, other = 0;
        int count = 0;
        foreach (ShiftPaymentGroup payment in payments)
        {
            count += payment.Count;
            switch (payment.Method)
            {
                case "cash": cash += payment.Amount; break;
                case "transfer": transfer += payment.Amount; break;
                case "card": card += payment.Amount; break;
                default: other += payment.Amount; break;
            }
        }
        return new(cash, transfer, card, other, count);
    }

    /// <summary>Valida el efectivo y la longitud máxima de la observación antes de consultar SQL.</summary>
    public static string ValidateCloseRequest(int operatorId, decimal countedCash, string note)
    {
        if (operatorId <= 0 || countedCash < 0 || countedCash > 999999999m
            || decimal.Truncate(countedCash * 100m) != countedCash * 100m)
            throw new ArgumentException("El efectivo contado debe ser positivo y tener como máximo dos decimales.");
        note = note.Trim();
        if (note.Length > 500) throw new ArgumentException("La observación excede 500 caracteres.");
        return note;
    }

    /// <summary>Calcula la diferencia y exige una explicación si el efectivo no cuadra.</summary>
    public static ShiftCloseAssessment EvaluateClose(ShiftTotals totals, decimal countedCash, string note)
    {
        decimal difference = countedCash - totals.Cash;
        if (difference != 0m && note.Length < 8)
            throw new ArgumentException("Explique la diferencia con al menos 8 caracteres.");
        RequireExactMoney(totals.Cash);
        RequireExactMoney(totals.Transfer);
        RequireExactMoney(totals.Card);
        RequireExactMoney(totals.Other);
        RequireExactMoney(difference);
        return new(difference, note);
    }

    /// <summary>Impide fracciones de centavo en un importe persistido.</summary>
    private static void RequireExactMoney(decimal amount)
    {
        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "El importe contiene fracciones de centavo.");
    }
}
