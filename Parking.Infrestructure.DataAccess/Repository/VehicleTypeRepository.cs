using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess.Repository;

public class vehicle_typeRepository : IBaseRepository<vehicle_type>, Ivehicle_typeRepository
{
    private readonly parking_dbContext _context;

    public vehicle_typeRepository(parking_dbContext context)
    {
        _context = context;
    }

    public async Task<vehicle_type> AddAsync(vehicle_type entity)
    {
        await _context.vehicle_types.AddAsync(entity);

        return entity;
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await _context.vehicle_types
            .FirstOrDefaultAsync(x => x.id == id);

        if (entity != null)
        {
            entity.is_deleted = true;
            entity.deleted_at = DateTime.Now;
        }
    }

    public async Task<IEnumerable<vehicle_type>> GetAllAsync()
    {
        return await _context.vehicle_types
            .AsNoTracking()
            .Where(x => !x.is_deleted)
            .OrderBy(x => x.name)
            .Select(x => new vehicle_type
            {
                id = x.id,

                name = x.name,
                icon = x.icon,

                hourly_rate = x.hourly_rate,

                grace_minutes = x.grace_minutes,
                fraction_minutes = x.fraction_minutes,
                fraction_rate = x.fraction_rate,

                is_active = x.is_active
            })
            .ToListAsync();
    }

    public async Task<vehicle_type?> GetByIdAsync(long id)
    {
        return await _context.vehicle_types
            .FirstOrDefaultAsync(x => x.id == id && !x.is_deleted);
    }

    public Task UpdateAsync(vehicle_type entity)
    {
        _context.vehicle_types.Update(entity);

        return Task.CompletedTask;
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task SoftDeleteAsync(vehicle_type entity)
    {
        _context.vehicle_types.Update(entity);

        await Task.CompletedTask;
    }
}