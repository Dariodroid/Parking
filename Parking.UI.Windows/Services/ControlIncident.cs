using System.IO;

namespace Parking.UI.Windows.Services;

/// <summary>Situación operativa que requiere revisión sin modificar la sesión ni el pago original.</summary>
public sealed record ControlIncident(string Key, string Category, string Plate, DateTime OccurredAt,
    string Detail, decimal Difference, string Operator, string? PhotoPath = null)
{
    /// <summary>La foto solo se muestra si existe en el equipo que consulta el centro.</summary>
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath) && File.Exists(PhotoPath);
}
