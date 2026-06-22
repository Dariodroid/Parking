using Parking.Domain.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Abstractions
{
    public interface IOperatorReportRepository
    {
        Task<List<OperatorReportItem>> GetReportAsync(DateTime fromDate,DateTime toDate);
    }
}
