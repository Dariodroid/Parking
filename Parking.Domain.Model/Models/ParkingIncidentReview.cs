namespace Parking.Domain.Model.Models;

/// <summary>Constancia de la revisión de una incidencia del centro de control.</summary>
public sealed class ParkingIncidentReview
{
    /// <summary>Clave estable de la incidencia revisada.</summary>
    public string IncidentKey { get; set; } = string.Empty;

    /// <summary>Fecha y hora local asignadas por SQL Server.</summary>
    public DateTime ReviewedAt { get; set; }

    /// <summary>Identificador del usuario que firmó la revisión.</summary>
    public int ReviewedBy { get; set; }

    /// <summary>Motivo escrito por el operador.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Usuario que atendió la incidencia.</summary>
    public user Reviewer { get; set; } = null!;
}
