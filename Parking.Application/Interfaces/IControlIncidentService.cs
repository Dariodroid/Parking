using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Consulta incidencias y registra las revisiones auditadas.</summary>
public interface IControlIncidentService
{
    Task<IReadOnlyList<ControlIncident>> GetOpenIncidentsAsync();
    Task ReviewAsync(string key, string reason, int operatorId);
    Task<IReadOnlyList<IncidentReview>> GetReviewsAsync();
}
