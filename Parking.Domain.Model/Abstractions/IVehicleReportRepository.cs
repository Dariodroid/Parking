using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Parking.Application.Dto;

namespace Parking.Domain.Model.Abstractions
{
    public interface IVehicleReportRepository
    {
        Task<List<VehicleReportDto>> GetReportAsync(
            VehicleReportFilterDto filter);
    }
}
