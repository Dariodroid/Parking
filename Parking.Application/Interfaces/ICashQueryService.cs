using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Consulta todos los cobros que se presentan juntos en Caja.</summary>
public interface ICashQueryService
{
    /// <summary>Devuelve los cobros confirmados en el intervalo, ordenados del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<CashMovement>> GetMovementsAsync(DateTime fromInclusive, DateTime toExclusive);
}
