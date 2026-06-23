using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.Dto
{
    public class VehicleReportFilterDto
    {
        public string? Plate { get; set; }

        public string? OwnerName { get; set; }

        public bool IncludeMonthly { get; set; } = true;

        public bool IncludeOccasional { get; set; } = true;

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }
    }
}
