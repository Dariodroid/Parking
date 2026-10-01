using Parking.Domain.Model.Models;
using Parking.Application.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.Interfaces
{
    /// <summary>Consulta los totales de cobro agrupados por operador.</summary>
    public interface IOperatorReportRepository
    {
        /// <summary>Calcula el informe en el intervalo indicado.</summary>
        /// <param name="fromDate">Inicio incluido.</param>
        /// <param name="toDate">Fin excluido.</param>
        /// <returns>Filas agrupadas por operador.</returns>
        Task<List<OperatorReportItem>> GetReportAsync(DateTime fromDate,DateTime toDate);
    }
}
