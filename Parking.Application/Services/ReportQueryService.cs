using Parking.Application.Dto;
using Parking.Application.Interfaces;

namespace Parking.Application.Services;

/// <summary>Coordina las consultas de informes manteniendo filtros y cálculos existentes.</summary>
public sealed class ReportQueryService : IReportQueryService
{
    private readonly IOperatorReportRepository _operators;
    private readonly IVehicleReportRepository _vehicles;
    private readonly IParkingPerformanceRepository _performance;

    public ReportQueryService(IOperatorReportRepository operators, IVehicleReportRepository vehicles,
        IParkingPerformanceRepository performance)
    {
        _operators = operators;
        _vehicles = vehicles;
        _performance = performance;
    }

    /// <inheritdoc />
    public Task<List<OperatorReportItem>> GetOperatorsAsync(DateTime fromInclusive, DateTime toExclusive)
        => _operators.GetReportAsync(fromInclusive, toExclusive);

    /// <inheritdoc />
    public Task<List<VehicleReportDto>> GetVehiclesAsync(VehicleReportFilterDto filter)
        => _vehicles.GetReportAsync(filter);

    /// <inheritdoc />
    public Task<ParkingPerformanceReport> GetPerformanceAsync(DateTime fromInclusive, DateTime toExclusive,
        DateTime observedAt) => _performance.GetAsync(fromInclusive, toExclusive, observedAt);
}
