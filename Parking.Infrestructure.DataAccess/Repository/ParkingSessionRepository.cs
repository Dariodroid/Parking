using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using System.Threading.Tasks;

namespace Parking.Infrastructure.DataAccess.Repository
{
    public class parking_sessionRepository : BaseRepository<parking_session>, Iparking_sessionRepository
    {
        public parking_sessionRepository(parking_dbContext context)
            : base(context)
        {
        }

        public async Task<parking_session?> GetActiveSessionByPlateAsync(string plate)
        {
            return await _context.Set<parking_session>()
                .FirstOrDefaultAsync(s => s.plate == plate
                                       && s.exit_time == null
                                       && !s.is_deleted);
        }

        public async Task<parking_session?> GetActiveSessionByQrAsync(string qrCode)
        {
            return await _context.Set<parking_session>()
                .FirstOrDefaultAsync(s => s.qr_data == qrCode
                                       && s.exit_time == null
                                       && !s.is_deleted);
        }
    }
}