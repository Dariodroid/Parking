using Microsoft.EntityFrameworkCore;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Infrastructure.DataAccess.Repository
{
    public class CashRepository : ICashRepository
    {
        private readonly parking_dbContext _context;

        public CashRepository(
            parking_dbContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetTotalIncomeAsync(
            DateTime fromDate,
            DateTime toDate)
        {
            return await _context.payments

                .AsNoTracking()

                .Where(x =>
                    !x.is_deleted &&
                    x.collected_at >= fromDate &&
                    x.collected_at <= toDate)

                .SumAsync(x => x.amount_paid);
        }

        /// <summary>Obtiene pagos no eliminados dentro de un intervalo de fechas con sus datos de sesión y operador.</summary>
        /// <param name="fromDate">Inicio inclusivo del intervalo.</param>
        /// <param name="toDate">Fin exclusivo del intervalo, normalmente el día posterior al seleccionado.</param>
        /// <returns>Pagos ordenados desde el más reciente.</returns>
        public async Task<List<payment>> GetPaymentsAsync(
     DateTime fromDate,
     DateTime toDate)
        {
            return await _context.payments

                .AsNoTracking()

                .Include(x => x.session)

                .Include(x => x.collected_byNavigation)

                // El fin exclusivo incluye todas las horas del último día sin solapar el siguiente.
                .Where(x =>
                    !x.is_deleted &&
                    x.collected_at >= fromDate &&
                    x.collected_at < toDate)

                .OrderByDescending(x => x.collected_at)

                .ToListAsync();
        }
    }
}
