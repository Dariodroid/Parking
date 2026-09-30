using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess;

/// <summary>Mapea cierres de turno conservando importes exactos en decimal.</summary>
public sealed class ParkingShiftClosureConfiguration : IEntityTypeConfiguration<ParkingShiftClosure>
{
    /// <summary>Declara todas las columnas monetarias con escala de dos decimales.</summary>
    /// <param name="entity">Constructor de la entidad de cierre.</param>
    public void Configure(EntityTypeBuilder<ParkingShiftClosure> entity)
    {
        // La identidad y el usuario permiten localizar el cierre firmado sin reescribir pagos.
        entity.ToTable("parking_shift_closures", "dbo");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(x => x.OperatorId).HasColumnName("operator_id");
        // Los límites de tiempo congelan qué cobros pertenecen al turno.
        entity.Property(x => x.StartedAt).HasColumnName("started_at").HasColumnType("datetime2");
        entity.Property(x => x.ClosedAt).HasColumnName("closed_at").HasColumnType("datetime2");
        // Todos los importes se guardan con dos decimales, incluido cualquier faltante o sobrante.
        entity.Property(x => x.ExpectedCash).HasColumnName("expected_cash").HasColumnType("decimal(18,2)");
        entity.Property(x => x.CountedCash).HasColumnName("counted_cash").HasColumnType("decimal(18,2)");
        entity.Property(x => x.Difference).HasColumnName("difference").HasColumnType("decimal(18,2)");
        entity.Property(x => x.TransferTotal).HasColumnName("transfer_total").HasColumnType("decimal(18,2)");
        entity.Property(x => x.CardTotal).HasColumnName("card_total").HasColumnType("decimal(18,2)");
        entity.Property(x => x.OtherTotal).HasColumnName("other_total").HasColumnType("decimal(18,2)");
        // El número de cobros y la observación completan la evidencia del cierre.
        entity.Property(x => x.PaymentCount).HasColumnName("payment_count");
        entity.Property(x => x.Note).HasColumnName("note").HasMaxLength(500).IsRequired();
        // Los últimos cierres de cada operador se consultan con este índice ordenado.
        entity.HasIndex(x => new { x.OperatorId, x.ClosedAt })
            .IsDescending(false, true).HasDatabaseName("IX_parking_shift_closures_operator_time");
        // La eliminación de un usuario no debe arrastrar cierres históricos.
        entity.HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
