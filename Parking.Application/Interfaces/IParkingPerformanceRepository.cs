using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Consulta datos históricos para el informe de ocupación y recaudación.</summary>
public interface IParkingPerformanceRepository
{
    /// <summary>Calcula la actividad diaria y los cobros del intervalo indicado.</summary>
    /// <param name="fromInclusive">Inicio local inclusivo.</param>
    /// <param name="toExclusive">Fin local exclusivo.</param>
    /// <param name="observedAt">Instante de emisión para limitar el día en curso.</param>
    /// <returns>Instantánea de capacidad y filas diarias.</returns>
    Task<ParkingPerformanceReport> GetAsync(DateTime fromInclusive, DateTime toExclusive, DateTime observedAt);
}
