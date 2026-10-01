using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Consulta incidencias y administra la revisión y cierre de caja.</summary>
public interface IOperationsControlService
{
    /// <summary>Obtiene incidencias operativas pendientes.</summary>
    Task<IReadOnlyList<ControlIncident>> GetOpenIncidentsAsync();

    /// <summary>Registra quién revisó una incidencia y por qué.</summary>
    /// <param name="key">Identificador de la incidencia.</param>
    /// <param name="reason">Motivo documentado.</param>
    /// <param name="operatorId">Operador responsable.</param>
    Task ReviewAsync(string key, string reason, int operatorId);

    /// <summary>Consulta las revisiones registradas.</summary>
    Task<IReadOnlyList<IncidentReview>> GetReviewsAsync();

    /// <summary>Calcula el turno abierto del operador.</summary>
    /// <param name="operatorId">Operador consultado.</param>
    Task<ShiftSummary> GetCurrentShiftAsync(int operatorId);

    /// <summary>Cierra el turno conservando importes esperados y contados.</summary>
    /// <param name="operatorId">Operador responsable.</param>
    /// <param name="countedCash">Efectivo contado.</param>
    /// <param name="note">Observación del cierre.</param>
    Task<ShiftClosure> CloseShiftAsync(int operatorId, decimal countedCash, string note);

    /// <summary>Consulta cierres de todos los operadores o de uno solo.</summary>
    /// <param name="operatorId">Operador, o nulo para todos.</param>
    Task<IReadOnlyList<ShiftClosure>> GetClosuresAsync(int? operatorId = null);
}
