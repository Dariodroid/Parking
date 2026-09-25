using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess.Repository;

public class ParkingSlotRepository : IParkingSlotRepository
{
    private readonly parking_dbContext _context;

    public ParkingSlotRepository(
        parking_dbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<parking_slot>> GetAllAsync()
    {
        return await _context.parking_slots
            .AsNoTracking()
            .OrderBy(x => x.slot_number)
            .ToListAsync();
    }

    public async Task<parking_slot?> GetByIdAsync(long id)
    {
        return await _context.parking_slots
            .Include(x => x.parking_sessions)
            .FirstOrDefaultAsync(x => x.id == id);
    }

    public async Task<parking_slot> AddAsync(
        parking_slot entity)
    {
        await _context.parking_slots
            .AddAsync(entity);

        return entity;
    }

    public Task UpdateAsync(
        parking_slot entity)
    {
        _context.parking_slots
            .Update(entity);

        return Task.CompletedTask;
    }

    public async Task DeleteAsync(long id)
    {
        var slot = await _context.parking_slots
            .FirstOrDefaultAsync(x => x.id == id);

        if (slot == null)
            return;

        _context.parking_slots
            .Remove(slot);
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<IEnumerable<parking_slot>> GetAvailableSlotsAsync()
    {
        return await _context.parking_slots
            .AsNoTracking()
            .Where(x => !x.is_occupied)
            .OrderBy(x => x.slot_number)
            .ToListAsync();
    }

    public async Task<parking_slot?> GetFirstAvailableSlotAsync()
    {
        return await _context.parking_slots
            .Where(x => !x.is_occupied)
            .OrderBy(x => x.updated_at)
            .ThenBy(x => x.slot_number)
            .FirstOrDefaultAsync();
    }

    public async Task<parking_slot?> GetAvailableSlotByIdAsync(int id)
    {
        return await _context.parking_slots
            .FirstOrDefaultAsync(x => x.id == id && !x.is_occupied);
    }
}
