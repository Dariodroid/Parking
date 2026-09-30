namespace Parking.Domain.Model.Models;

/// <summary>Instantánea firmada del efectivo contado y los cobros de un turno.</summary>
public sealed class ParkingShiftClosure
{
    /// <summary>Identificador del cierre.</summary>
    public long Id { get; set; }

    /// <summary>Usuario que realizó el cierre.</summary>
    public int OperatorId { get; set; }

    /// <summary>Inicio del intervalo cerrado.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Fin del intervalo cerrado.</summary>
    public DateTime ClosedAt { get; set; }

    /// <summary>Efectivo que debía existir según los cobros registrados.</summary>
    public decimal ExpectedCash { get; set; }

    /// <summary>Efectivo físico declarado por el operador.</summary>
    public decimal CountedCash { get; set; }

    /// <summary>Diferencia entre efectivo contado y esperado.</summary>
    public decimal Difference { get; set; }

    /// <summary>Total cobrado mediante transferencia.</summary>
    public decimal TransferTotal { get; set; }

    /// <summary>Total cobrado mediante tarjeta.</summary>
    public decimal CardTotal { get; set; }

    /// <summary>Total cobrado mediante otros medios.</summary>
    public decimal OtherTotal { get; set; }

    /// <summary>Cantidad de cobros incluidos en el turno.</summary>
    public int PaymentCount { get; set; }

    /// <summary>Observación del cierre.</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>Operador que firmó el cierre.</summary>
    public user Operator { get; set; } = null!;
}
