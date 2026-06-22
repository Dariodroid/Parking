using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Models
{
    public class OperatorReportItem
    {
        public string OperatorName { get; set; }

        public int TotalPayments { get; set; }

        public decimal TotalAmount { get; set; }
    }
}
