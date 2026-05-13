using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess.Repository
{
    public class VehicleTypeRepository : IBaseRepository<VehicleType>, IVehicleTypeRepository
    {
        private readonly parking_dbContext _context;

        public VehicleTypeRepository(parking_dbContext context)
        {
            _context = context;
        }

        public async Task<VehicleType> AddAsync(VehicleType entity)
        {
            await _context.VehicleTypes.AddAsync(entity);
            return entity;
        }

        public async Task DeleteAsync(long id)
        {
            var entity = await _context.VehicleTypes
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity != null)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.Now;
            }
        }

        public async Task<IEnumerable<VehicleType>> GetAllAsync()
        {
            return await _context.VehicleTypes
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .Select(x => new VehicleType
                {
                    Id = x.Id,
                    Name = x.Name,
                    Icon = x.Icon,
                    HourlyRate = x.HourlyRate,
                    IsActive = x.IsActive
                })
                .ToListAsync();
        }

        public async Task<VehicleType?> GetByIdAsync(long id)
        {
            return await _context.VehicleTypes
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        public Task UpdateAsync(VehicleType entity)
        {
            _context.VehicleTypes.Update(entity);
            return Task.CompletedTask;
        }

        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task SoftDeleteAsync(VehicleType entity)
        {
            _context.VehicleTypes.Update(entity);
            await Task.CompletedTask;
        }
    }
}