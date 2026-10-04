namespace Parking.Application.Dto;

/// <summary>Lecturas del centro de control obtenidas en DataAccess.</summary>
public sealed record ControlIncidentData(IReadOnlyList<ControlSessionData> Sessions,
    IReadOnlyList<ControlOverduePlanData> OverduePlans, IReadOnlySet<string> ReviewedKeys);
