using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using System.Threading.Tasks;

namespace Parking.Infrastructure.DataAccess.Repository
{
    public class ParkingSessionRepository : BaseRepository<ParkingSession>, IParkingSessionRepository
    {
        public ParkingSessionRepository(parking_dbContext context)
            : base(context)
        {
        }

        public async Task<ParkingSession?> GetActiveSessionByPlateAsync(string plate)
        {
            return await _context.Set<ParkingSession>()
                .FirstOrDefaultAsync(s => s.plate == plate
                                       && s.exit_time == null
                                       && !s.is_deleted);
        }

        public async Task<ParkingSession?> GetActiveSessionByQrAsync(string qrCode)
        {
            return await _context.Set<ParkingSession>()
                .FirstOrDefaultAsync(s => s.qr_data == qrCode
                                       && s.exit_time == null
                                       && !s.is_deleted);
        }
    }
}