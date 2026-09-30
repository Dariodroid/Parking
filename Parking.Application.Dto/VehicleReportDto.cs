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

        /// <summary>Modalidad de cobro aplicada a las estancias incluidas en esta fila.</summary>
        public string AccessSummary { get; set; } = "";

        public string PlanStatus { get; set; } = "";

        /// <summary>Indica si la ficha del cliente permite usar el plan actualmente.</summary>
        public bool VehicleIsActive { get; set; }

        /// <summary>Indica si el plan está habilitado, independientemente de sus fechas.</summary>
        public bool PlanIsActive { get; set; }

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
