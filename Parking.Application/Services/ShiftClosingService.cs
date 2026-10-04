using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Policies;

namespace Parking.Application.Services;

/// <summary>Coordina los datos del turno y las reglas de Domain para el cierre de caja.</summary>
public sealed class ShiftClosingService : IShiftClosingService
{
    private readonly IShiftClosingRepository _repository;

    /// <summary>Recibe el repositorio que lee y conserva los cierres.</summary>
    public ShiftClosingService(IShiftClosingRepository repository) => _repository = repository;

    /// <summary>Calcula el turno abierto del operador.</summary>
    public async Task<ShiftSummary> GetCurrentShiftAsync(int operatorId) =>
        CalculateShift(await _repository.GetCurrentShiftSourceAsync(operatorId));

    /// <summary>Coordina la decisión de Domain con los pagos de la transacción de cierre.</summary>
    public Task<ShiftClosure> CloseShiftAsync(int operatorId, decimal countedCash, string note)
    {
        string validatedNote = ShiftClosingPolicy.ValidateCloseRequest(operatorId, countedCash, note);

        // DataAccess invoca la regla de Domain con la lectura hecha dentro de su transacción serializable.
        return _repository.CloseShiftAsync(operatorId, source =>
        {
            ShiftSummary summary = CalculateShift(source);
            ShiftTotals totals = new(summary.Cash, summary.Transfer, summary.Card,
                summary.Other, summary.PaymentCount);
            ShiftCloseAssessment assessment = ShiftClosingPolicy.EvaluateClose(
                totals, countedCash, validatedNote);
            return new ShiftClosingDecision(summary, countedCash, assessment.Difference, assessment.Note);
        });
    }

    /// <summary>Consulta cierres históricos de todos los operadores o de uno solo.</summary>
    public Task<IReadOnlyList<ShiftClosure>> GetClosuresAsync(int? operatorId = null) =>
        _repository.GetClosuresAsync(operatorId);

    /// <summary>Traduce la clasificación de Domain al resultado usado por la UI.</summary>
    private static ShiftSummary CalculateShift(ShiftSource source)
    {
        ShiftTotals totals = ShiftClosingPolicy.CalculateShift(source.Payments.Select(p =>
            new ShiftPaymentGroup(p.Method, p.Amount, p.Count)));
        return new ShiftSummary(source.StartedAt, source.EndedAt, totals.Cash,
            totals.Transfer, totals.Card, totals.Other, totals.PaymentCount);
    }
}
