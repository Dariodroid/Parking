using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Lee pagos y conserva cierres de turno en DataAccess.</summary>
public interface IShiftClosingRepository
{
    Task<ShiftSource> GetCurrentShiftSourceAsync(int operatorId);
    Task<ShiftClosure> CloseShiftAsync(int operatorId, Func<ShiftSource, ShiftClosingDecision> decide);
    Task<IReadOnlyList<ShiftClosure>> GetClosuresAsync(int? operatorId = null);
}
