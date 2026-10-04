using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Consulta y cierra turnos de caja.</summary>
public interface IShiftClosingService
{
    Task<ShiftSummary> GetCurrentShiftAsync(int operatorId);
    Task<ShiftClosure> CloseShiftAsync(int operatorId, decimal countedCash, string note);
    Task<IReadOnlyList<ShiftClosure>> GetClosuresAsync(int? operatorId = null);
}
