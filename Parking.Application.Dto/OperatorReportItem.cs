using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.Dto
{
    /// <summary>Totales de pagos correspondientes a un operador del informe.</summary>
    public class OperatorReportItem
    {
        /// <summary>Nombre visible del operador.</summary>
        public string OperatorName { get; set; } = string.Empty;

        /// <summary>Cantidad de pagos recibidos.</summary>
        public int TotalPayments { get; set; }

        /// <summary>Importe total de esos pagos.</summary>
        public decimal TotalAmount { get; set; }
    }
}
