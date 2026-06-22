using Parking.Domain.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Abstractions
{
    public interface ICashRepository
    {
        Task<decimal> GetTotalIncomeAsync(
            DateTime fromDate,
            DateTime toDate);

        Task<List<payment>> GetPaymentsAsync(
            DateTime fromDate,
            DateTime toDate);
    }
}
