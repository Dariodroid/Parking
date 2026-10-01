using Parking.Domain.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.Interfaces
{
    /// <summary>Consulta cobros de salida para la pantalla de Caja.</summary>
    public interface ICashRepository
    {
        /// <summary>Suma pagos confirmados dentro del intervalo.</summary>
        /// <param name="fromDate">Inicio incluido.</param>
        /// <param name="toDate">Fin excluido.</param>
        /// <returns>Importe total exacto.</returns>
        Task<decimal> GetTotalIncomeAsync(
            DateTime fromDate,
            DateTime toDate);

        /// <summary>Lista los cobros de salidas del intervalo.</summary>
        /// <param name="fromDate">Inicio incluido.</param>
        /// <param name="toDate">Fin excluido.</param>
        /// <returns>Pagos persistidos en el periodo.</returns>
        Task<List<payment>> GetPaymentsAsync(
            DateTime fromDate,
            DateTime toDate);
    }
}
