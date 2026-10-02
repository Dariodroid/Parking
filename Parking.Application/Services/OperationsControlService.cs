using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Policies;

namespace Parking.Application.Services;

/// <summary>Coordina las lecturas SQL, las reglas de Domain y las respuestas del centro de control.</summary>
public sealed class OperationsControlService : IOperationsControlService
{
    private readonly IOperationsControlRepository _repository;
    private readonly ICurrencyFormatter _currencyFormatter;

    /// <summary>Recibe la persistencia y el formato monetario de los avisos.</summary>
    public OperationsControlService(IOperationsControlRepository repository, ICurrencyFormatter currencyFormatter)
    {
        _repository = repository;
        _currencyFormatter = currencyFormatter;
    }

    /// <summary>Presenta las incidencias decididas por Domain con sus datos y textos de interfaz.</summary>
    public async Task<IReadOnlyList<ControlIncident>> GetOpenIncidentsAsync()
    {
        DateTime now = DateTime.Now;
        ControlIncidentData data = await _repository.GetIncidentDataAsync(
            OperationsControlPolicy.PaymentReviewSince(now), now.Date);
        var incidents = new List<ControlIncident>();
        foreach (ControlSessionData session in data.Sessions)
        {
            ControlIncidentFinding? finding = OperationsControlPolicy.EvaluateSession(session.Status,
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
            ControlIncidentFinding? finding = OperationsControlPolicy.EvaluateOverdueFee(
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
        string validatedReason = OperationsControlPolicy.ValidateReview(key, reason, operatorId);
        return _repository.ReviewAsync(key, validatedReason, operatorId);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IncidentReview>> GetReviewsAsync() => _repository.GetReviewsAsync();

    /// <inheritdoc />
    public async Task<ShiftSummary> GetCurrentShiftAsync(int operatorId) =>
        CalculateShift(await _repository.GetCurrentShiftSourceAsync(operatorId));

    /// <summary>Coordina la decisión de Domain con los pagos de la transacción de cierre.</summary>
    public Task<ShiftClosure> CloseShiftAsync(int operatorId, decimal countedCash, string note)
    {
        string validatedNote = OperationsControlPolicy.ValidateCloseRequest(operatorId, countedCash, note);

        // DataAccess invoca la regla de Domain con la lectura hecha dentro de su transacción serializable.
        return _repository.CloseShiftAsync(operatorId, source =>
        {
            ShiftSummary summary = CalculateShift(source);
            ShiftTotals totals = new(summary.Cash, summary.Transfer, summary.Card,
                summary.Other, summary.PaymentCount);
            ShiftCloseAssessment assessment = OperationsControlPolicy.EvaluateClose(
                totals, countedCash, validatedNote);
            return new ShiftClosingDecision(summary, countedCash, assessment.Difference, assessment.Note);
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ShiftClosure>> GetClosuresAsync(int? operatorId = null) =>
        _repository.GetClosuresAsync(operatorId);

    /// <summary>Traduce la clasificación de Domain al resultado usado por la UI.</summary>
    private static ShiftSummary CalculateShift(ShiftSource source)
    {
        ShiftTotals totals = OperationsControlPolicy.CalculateShift(source.Payments.Select(p =>
            new ShiftPaymentGroup(p.Method, p.Amount, p.Count)));
        return new ShiftSummary(source.StartedAt, source.EndedAt, totals.Cash,
            totals.Transfer, totals.Card, totals.Other, totals.PaymentCount);
    }
}
