using Microsoft.EntityFrameworkCore;
using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.Infrastructure.DataAccess;
using Parking.Infrastructure.DataAccess.Entities;

namespace Parking.Infrastructure.DataAccess.Repository;

public class ParkingDashboardRepository : IParkingDashboard
{
    private readonly parking_dbContext _context;

    public ParkingDashboardRepository(parking_dbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ParkingSlotDashboardItemDTO>> GetDashboardSlotsAsync()
    {
        return await _context.parking_slots
            .AsNoTracking()
            .Include(x => x.current_session)
            .ThenInclude(x => x.registered_vehicle)
            .Include(x => x.current_session).ThenInclude(x => x.vehicle_type)
            .OrderBy(x => x.slot_number)
            .Select(x => new ParkingSlotDashboardItemDTO
            {
                SlotId = x.id,
                SlotNumber = x.slot_number,
                IsOccupied = x.is_occupied,
                Plate = x.current_session != null ? x.current_session.plate : null,
                OwnerName = x.current_session != null && x.current_session.registered_vehicle != null
                    ? x.current_session.registered_vehicle.owner_name
                    : null,
                VehicleType = x.current_session != null && x.current_session.vehicle_type != null
                    ? x.current_session.vehicle_type.name
                    : null,
                EntryTime = x.current_session != null ? x.current_session.entry_time : null
            }).ToListAsync();
    }

    public async Task<Dictionary<int, (int X, int Y)>> GetSlotPositionsAsync()
    {
        var positions = new Dictionary<int, (int X, int Y)>();

        try
        {
            using var localContext = new LocalPositionDbContext();
            await localContext.Database.EnsureCreatedAsync();

            var savedPositions = await localContext.SlotPositions.ToListAsync();

            foreach (var pos in savedPositions)
            {
                positions[pos.SlotId] = (pos.PositionX, pos.PositionY);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error leyendo posiciones: {ex.Message}");
        }

        return positions;
    }

    public async Task UpdateSlotPositionsAsync(Dictionary<int, (int X, int Y)> positions)
    {
        try
        {
            using var localContext = new LocalPositionDbContext();
            await localContext.Database.EnsureCreatedAsync();

            localContext.SlotPositions.RemoveRange(localContext.SlotPositions);

            foreach (var kvp in positions)
            {
                localContext.SlotPositions.Add(new SlotPositionEntity
                {
                    SlotId = kvp.Key,
                    PositionX = kvp.Value.X,
                    PositionY = kvp.Value.Y
                });
            }

            await localContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error guardando posiciones: {ex.Message}");
        }
    }
}