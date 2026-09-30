namespace Parking.Domain.Model.Models;

/// <summary>Cobro confirmado de una cuota mensual vencida, independiente del pago de una salida.</summary>
public sealed class ParkingMonthlyFeeReceipt
{
    /// <summary>Identificador del asiento contable.</summary>
    public long Id { get; set; }

    /// <summary>Contrato cuya cuota fue cobrada.</summary>
    public int PlanId { get; set; }

    /// <summary>Placa conservada como instantánea al momento del cobro.</summary>
    public string Plate { get; set; } = string.Empty;

    /// <summary>Importe exacto en unidades monetarias con dos decimales.</summary>
    public decimal Amount { get; set; }

    /// <summary>Medio de cobro: cash, transfer o card.</summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>Usuario que confirmó la recepción del dinero.</summary>
    public int CollectedBy { get; set; }

    /// <summary>Momento local en que se registró el cobro.</summary>
    public DateTime CollectedAt { get; set; }

    /// <summary>Fecha final del período vencido que se pagó.</summary>
    public DateTime PeriodEndDate { get; set; }

    /// <summary>Contrato asociado al cobro.</summary>
    public vehicle_monthly_plan Plan { get; set; } = null!;

    /// <summary>Operador que registró el cobro.</summary>
    public user Collector { get; set; } = null!;
}
