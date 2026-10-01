namespace Parking.Application.Dto;

/// <summary>Constancia de quién atendió una incidencia y por qué.</summary>
public sealed record IncidentReview(string IncidentKey, DateTime ReviewedAt, string Reviewer, string Reason);
