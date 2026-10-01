using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Consulta los vehículos que corresponden a los filtros de un informe.</summary>
public interface IVehicleReportRepository
{
    /// <summary>Obtiene las filas del informe detallado de vehículos.</summary>
    /// <param name="filter">Criterios aplicados por el usuario.</param>
    /// <returns>Filas que se mostrarán y exportarán.</returns>
    Task<List<VehicleReportDto>> GetReportAsync(VehicleReportFilterDto filter);
}
