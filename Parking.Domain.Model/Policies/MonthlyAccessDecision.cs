namespace Parking.Domain.Model.Policies;

/// <summary>Resultado de evaluar el contrato antes de abrir una sesión de estacionamiento.</summary>
/// <param name="Kind">Clasificación que determina si se genera ticket y se cobra estancia.</param>
/// <param name="PendingFee">Cuota mensual vencida pendiente según la fecha de pago registrada.</param>
public sealed record MonthlyAccessDecision(MonthlyAccessKind Kind, decimal PendingFee)
{
    /// <summary>Indica que la sesión se abrirá como mensualizada, sin ticket ni cobro de estancia.</summary>
    public bool IsMonthly => Kind == MonthlyAccessKind.Monthly;
}
