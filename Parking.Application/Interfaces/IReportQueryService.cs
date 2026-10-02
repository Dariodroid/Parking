using Parking.Application.Dto;

namespace Parking.Application.Interfaces;

/// <summary>Consultas de los tres informes operativos sin exponer repositorios a la UI.</summary>
public interface IReportQueryService
{
    Task<List<OperatorReportItem>> GetOperatorsAsync(DateTime fromInclusive, DateTime toExclusive);
    Task<List<VehicleReportDto>> GetVehiclesAsync(VehicleReportFilterDto filter);
    Task<ParkingPerformanceReport> GetPerformanceAsync(DateTime fromInclusive, DateTime toExclusive,
        DateTime observedAt);
}
