namespace Parking.Domain.Model.Policies;

/// <summary>Calcula el turno y comprueba los importes antes de cerrar caja.</summary>
public static class ShiftClosingPolicy
{
    /// <summary>El turno empieza en el último cierre del día, o a medianoche si no existe.</summary>
    public static DateTime DetermineShiftStart(DateTime lastClosed, DateTime now) =>
        lastClosed > now.Date ? lastClosed : now.Date;

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
