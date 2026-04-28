using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Models; // Asegúrate de que apunte a tus entidades

namespace Parking.Infrastructure.DataAccess
{
    public partial class ParkingDbContext : DbContext
    {
        public ParkingDbContext(DbContextOptions<ParkingDbContext> options)
            : base(options)
        {
        }

        // DbSets - Estos son los que usarás en tus repositorios
        public virtual DbSet<FingerprintTemplate> FingerprintTemplates { get; set; }
        public virtual DbSet<ParkingSession> ParkingSessions { get; set; }
        public virtual DbSet<Payment> Payments { get; set; }
        public virtual DbSet<RegisteredVehicle> RegisteredVehicles { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<VehicleType> VehicleTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ============================================================
            // CONFIGURACIÓN AUTOMATIZADA (Extraída de tu archivo generado)
            // ============================================================

            modelBuilder.Entity<ParkingSession>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK__parking___3213E83F56D9C37C");
                entity.ToTable("parking_sessions");
                entity.HasIndex(e => e.SessionCode, "UQ__parking___615A1EA73E5AD186").IsUnique();

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.AmountDue).HasColumnType("decimal(10, 2)").HasColumnName("amount_due");
                entity.Property(e => e.ChargeableMinutes).HasColumnName("chargeable_minutes");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())").HasColumnName("created_at");
                entity.Property(e => e.CreatedBy).HasColumnName("created_by");
                entity.Property(e => e.DurationMinutes).HasColumnName("duration_minutes");
                entity.Property(e => e.EntryOperatorId).HasColumnName("entry_operator_id");
                entity.Property(e => e.EntryPhotoPath).HasMaxLength(500).HasColumnName("entry_photo_path");
                entity.Property(e => e.EntryTime).HasColumnName("entry_time");
                entity.Property(e => e.ExitOperatorId).HasColumnName("exit_operator_id");
                entity.Property(e => e.ExitTime).HasColumnName("exit_time");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.Plate).IsRequired().HasMaxLength(20).HasColumnName("plate");
                entity.Property(e => e.SessionCode).IsRequired().HasMaxLength(20).HasColumnName("session_code");
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasColumnName("status");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
                entity.Property(e => e.VehicleTypeId).HasColumnName("vehicle_type_id");
                entity.Property(e => e.RegisteredVehicleId).HasColumnName("registered_vehicle_id");
                entity.Property(e => e.QrData).HasColumnName("qr_data");
                entity.Property(e => e.Notes).HasColumnName("notes");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");

                // Relaciones corregidas para evitar conflictos de múltiples operadores
                entity.HasOne(d => d.EntryOperator).WithMany(p => p.ParkingSessionEntryOperators)
                    .HasForeignKey(d => d.EntryOperatorId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(d => d.ExitOperator).WithMany(p => p.ParkingSessionExitOperators)
                    .HasForeignKey(d => d.ExitOperatorId);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK__users__3213E83FF10AB662");
                entity.ToTable("users");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(120).HasColumnName("full_name");
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50).HasColumnName("username");
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(256).HasColumnName("password_hash");
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20).HasColumnName("role");
                entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())").HasColumnName("created_at");
            });

            modelBuilder.Entity<RegisteredVehicle>(entity =>
            {
                entity.ToTable("registered_vehicles");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Plate).IsRequired().HasMaxLength(20).HasColumnName("plate");
                entity.Property(e => e.VehicleTypeId).HasColumnName("vehicle_type_id");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            });

            modelBuilder.Entity<VehicleType>(entity =>
            {
                entity.ToTable("vehicle_types");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(60).HasColumnName("name");
                entity.Property(e => e.HourlyRate).HasColumnType("decimal(10, 2)").HasColumnName("hourly_rate");
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.ToTable("payments");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.AmountDue).HasColumnType("decimal(10, 2)").HasColumnName("amount_due");
                entity.Property(e => e.SessionId).HasColumnName("session_id");
            });

            modelBuilder.Entity<FingerprintTemplate>(entity =>
            {
                entity.ToTable("fingerprint_templates");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.VehicleId).HasColumnName("vehicle_id");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}