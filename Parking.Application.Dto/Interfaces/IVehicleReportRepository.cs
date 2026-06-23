using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.Dto.Interfaces
{
    public interface IVehicleReportRepository
    {
        Task<List<VehicleReportDto>> GetReportAsync(
            VehicleReportFilterDto filter);
    }
}
