using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Lee datos de incidencias y conserva sus revisiones.</summary>
public interface IControlIncidentRepository
{
    Task<ControlIncidentData> GetIncidentDataAsync(DateTime since, DateTime today);
    Task ReviewAsync(string key, string reason, int operatorId);
    Task<IReadOnlyList<IncidentReview>> GetReviewsAsync();
}
