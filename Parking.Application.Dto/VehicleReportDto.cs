using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.Dto
{
    public class VehicleReportDto
    {
        public string Plate { get; set; } = "";

        public string OwnerName { get; set; } = "";

        public string VehicleType { get; set; } = "";

        public string Category { get; set; } = "";

        public string PlanStatus { get; set; } = "";

        public decimal MonthlyFee { get; set; }

        public DateTime? PlanStartDate { get; set; }

        public DateTime? PlanEndDate { get; set; }

        public int TotalEntries { get; set; }

        public DateTime? LastEntryDate { get; set; }

        public decimal TotalCollected { get; set; }

        public int TotalMinutesParked { get; set; }

        public DateTime? LastExitDate { get; set; }

        public string CurrentStatus { get; set; } = string.Empty;
    }
}
