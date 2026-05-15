using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using System.Threading.Tasks;

namespace Parking.Infrastructure.DataAccess.Repository
{
    public class RegisteredVehicleRepository
        : BaseRepository<registered_vehicle>,
          IRegisteredVehicle
    {
        private readonly parking_dbContext _context;

        public RegisteredVehicleRepository(parking_dbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<bool> ExistsByPlateAsync(string plate)
        {
            return await _context.registered_vehicles
                .AnyAsync(x =>
                    x.plate == plate &&
                    !x.is_deleted);
        }

        public async Task SoftDeleteAsync(
            registered_vehicle entity,
            int deletedBy)
        {
            entity.is_deleted = true;

            entity.deleted_at = DateTime.Now;

            entity.deleted_by = deletedBy;

            _context.registered_vehicles.Update(entity);

            await Task.CompletedTask;
        }
    }
}