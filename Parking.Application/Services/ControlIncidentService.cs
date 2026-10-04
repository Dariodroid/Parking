using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Policies;

namespace Parking.Application.Services;

/// <summary>Aplica reglas de Domain a los datos de incidencias y registra revisiones.</summary>
public sealed class ControlIncidentService : IControlIncidentService
{
    private readonly IControlIncidentRepository _repository;
    private readonly ICurrencyFormatter _currencyFormatter;

    /// <summary>Recibe la persistencia y el formato monetario de los avisos.</summary>
    public ControlIncidentService(IControlIncidentRepository repository, ICurrencyFormatter currencyFormatter)
    {
        _repository = repository;
        _currencyFormatter = currencyFormatter;
    }

    /// <summary>Presenta las incidencias decididas por Domain con sus datos y textos de interfaz.</summary>
    public async Task<IReadOnlyList<ControlIncident>> GetOpenIncidentsAsync()
    {
        DateTime now = DateTime.Now;
        ControlIncidentData data = await _repository.GetIncidentDataAsync(
            ControlIncidentPolicy.PaymentReviewSince(now), now.Date);
        var incidents = new List<ControlIncident>();
        foreach (ControlSessionData session in data.Sessions)
        {
            ControlIncidentFinding? finding = ControlIncidentPolicy.EvaluateSession(session.Status,
                session.EntryTime, session.ExitTime, session.HasRegisteredVehicle,
                session.AmountDue, session.PaidTotal, now);
            if (finding?.Kind == ControlIncidentKind.ProlongedSession)
            {
                incidents.Add(new ControlIncident($"stale:{session.Id}", "Sesión prolongada", session.Plate,
                    finding.OccurredAt, "Entrada sin salida registrada durante más de 48 horas.", 0m,
                    session.EntryOperator, session.PhotoPath));
            }
            else if (finding?.Kind == ControlIncidentKind.PaymentDifference)
                incidents.Add(new ControlIncident($"payment:{session.Id}", "Diferencia de cobro", session.Plate,
                    finding.OccurredAt,
                    $"Debido: {_currencyFormatter.Format(session.AmountDue ?? 0m)} · registrado en payments: {_currencyFormatter.Format(session.PaidTotal)}.",
                    finding.Difference, session.ExitOperator, session.PhotoPath));
        }
        foreach (ControlOverduePlanData plan in data.OverduePlans)
        {
            ControlIncidentFinding? finding = ControlIncidentPolicy.EvaluateOverdueFee(
                plan.EndDate, plan.PaymentDate, plan.MonthlyFee, now.Date);
            if (finding is not null)
                incidents.Add(new ControlIncident($"debt:{plan.Id}", "Cuota vencida", plan.Plate,
                    finding.OccurredAt,
                    $"{plan.OwnerName}: cuota pendiente del contrato vencido el {plan.EndDate:dd/MM/yyyy}.",
                    finding.Difference, string.Empty));
        }

        return incidents.Where(i => !data.ReviewedKeys.Contains(i.Key))
            .OrderByDescending(i => Math.Abs(i.Difference)).ThenBy(i => i.OccurredAt).ToList();
    }

    /// <summary>Pide a Domain validar la explicación antes de registrarla.</summary>
    public Task ReviewAsync(string key, string reason, int operatorId)
    {
        string validatedReason = ControlIncidentPolicy.ValidateReview(key, reason, operatorId);
        return _repository.ReviewAsync(key, validatedReason, operatorId);
    }

    /// <summary>Consulta las revisiones registradas.</summary>
    public Task<IReadOnlyList<IncidentReview>> GetReviewsAsync() => _repository.GetReviewsAsync();
}
