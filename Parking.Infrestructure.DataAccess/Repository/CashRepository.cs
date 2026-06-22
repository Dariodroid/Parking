using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Abstractions;
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

        public async Task<List<payment>> GetPaymentsAsync(
     DateTime fromDate,
     DateTime toDate)
        {
            return await _context.payments

                .AsNoTracking()

                .Include(x => x.session)

                .Include(x => x.collected_byNavigation)

                .Where(x =>
                    !x.is_deleted &&
                    x.collected_at >= fromDate &&
                    x.collected_at <= toDate)

                .OrderByDescending(x => x.collected_at)

                .ToListAsync();
        }
    }
}
