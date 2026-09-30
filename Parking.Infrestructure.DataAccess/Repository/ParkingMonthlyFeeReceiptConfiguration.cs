using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess;

/// <summary>Hace coincidir el modelo EF con el libro SQL de cuotas mensuales.</summary>
public sealed class ParkingMonthlyFeeReceiptConfiguration : IEntityTypeConfiguration<ParkingMonthlyFeeReceipt>
{
    /// <summary>Declara nombres, tipos, clave única y relaciones sin modificar el contexto generado.</summary>
    /// <param name="entity">Constructor de la entidad de cobros mensuales.</param>
    public void Configure(EntityTypeBuilder<ParkingMonthlyFeeReceipt> entity)
    {
        // SQL impide registrar importes nulos o negativos.
        entity.ToTable("parking_monthly_fee_receipts", "dbo",
            table => table.HasCheckConstraint("CK_parking_monthly_fee_receipt_amount", "[amount] > 0"));
        // La identidad bigint es la clave del asiento y el plan indica a qué contrato pertenece.
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(x => x.PlanId).HasColumnName("plan_id");
        // La placa se conserva en el asiento aunque cambie posteriormente la ficha del cliente.
        entity.Property(x => x.Plate).HasColumnName("plate").HasMaxLength(20).IsRequired();
        // decimal(10,2) coincide con la cuota del contrato y evita fracciones ocultas de centavo.
        entity.Property(x => x.Amount).HasColumnName("amount").HasColumnType("decimal(10,2)");
        // El medio, el operador y el momento permiten conciliar el cobro en Caja.
        entity.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(20).IsRequired();
        entity.Property(x => x.CollectedBy).HasColumnName("collected_by");
        entity.Property(x => x.CollectedAt).HasColumnName("collected_at").HasColumnType("datetime2");
        // La fecha del período se guarda sin hora para impedir dos cobros de la misma cuota.
        entity.Property(x => x.PeriodEndDate).HasColumnName("period_end_date").HasColumnType("date");

        // La cuota de un mismo contrato y período solo puede registrarse una vez.
        entity.HasAlternateKey(x => new { x.PlanId, x.PeriodEndDate })
            .HasName("UQ_parking_monthly_fee_receipt_period");
        // El índice acelera la búsqueda de Caja por fechas de cobro.
        entity.HasIndex(x => x.CollectedAt).HasDatabaseName("IX_parking_monthly_fee_receipts_collected_at");
        // Las referencias no eliminan asientos históricos cuando cambia un contrato o usuario.
        entity.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne(x => x.Collector).WithMany().HasForeignKey(x => x.CollectedBy)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
