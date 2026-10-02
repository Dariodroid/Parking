using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Acceso persistente a incidencias, revisiones y cierres de turno.</summary>
public interface IOperationsControlRepository
{
    /// <summary>Consulta los datos necesarios para detectar incidencias.</summary>
    Task<ControlIncidentData> GetIncidentDataAsync(DateTime since, DateTime today);

    /// <summary>Guarda la revisión auditada de una incidencia.</summary>
    Task ReviewAsync(string key, string reason, int operatorId);

    /// <summary>Consulta las revisiones registradas.</summary>
    Task<IReadOnlyList<IncidentReview>> GetReviewsAsync();

    /// <summary>Consulta pagos del turno abierto, agrupados por medio.</summary>
    Task<ShiftSource> GetCurrentShiftSourceAsync(int operatorId);

    /// <summary>Valida con Application y guarda el cierre dentro de una sola transacción.</summary>
    Task<ShiftClosure> CloseShiftAsync(int operatorId, Func<ShiftSource, ShiftClosingDecision> decide);

    /// <summary>Consulta cierres históricos.</summary>
    Task<IReadOnlyList<ShiftClosure>> GetClosuresAsync(int? operatorId = null);
}
