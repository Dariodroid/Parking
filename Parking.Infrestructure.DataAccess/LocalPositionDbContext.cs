using Microsoft.EntityFrameworkCore;
using Parking.Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore.Sqlite;
using System.IO;

namespace Parking.Infrastructure.DataAccess;

public class LocalPositionDbContext : DbContext
{
    public DbSet<SlotPositionEntity> SlotPositions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ParkingApp",
            "slot_positions.db"
        );

        var directory = Path.GetDirectoryName(dbPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }

        optionsBuilder.UseSqlite($"Data Source={dbPath}"); // Ensure SQLite package is installed
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SlotPositionEntity>()
            .ToTable("slot_positions");

        modelBuilder.Entity<SlotPositionEntity>()
            .HasKey(x => x.SlotId);
    }
}