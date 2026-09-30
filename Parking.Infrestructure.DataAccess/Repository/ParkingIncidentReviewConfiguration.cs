using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess;

/// <summary>Mapea las revisiones firmadas de incidencias a su tabla SQL.</summary>
public sealed class ParkingIncidentReviewConfiguration : IEntityTypeConfiguration<ParkingIncidentReview>
{
    /// <summary>Configura la clave textual, el sello de tiempo y el operador responsable.</summary>
    /// <param name="entity">Constructor de la entidad de revisión.</param>
    public void Configure(EntityTypeBuilder<ParkingIncidentReview> entity)
    {
        // Cada incidencia puede tener una revisión conservada bajo su clave estable.
        entity.ToTable("parking_incident_reviews", "dbo");
        entity.HasKey(x => x.IncidentKey);
        entity.Property(x => x.IncidentKey).HasColumnName("incident_key").HasMaxLength(100);
        // SQL asigna la fecha real de revisión; el operador y el motivo quedan obligatorios.
        entity.Property(x => x.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("datetime2")
            .HasDefaultValueSql("(SYSDATETIME())");
        entity.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
        entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        // Una cuenta no puede borrar en cascada la evidencia firmada de revisiones anteriores.
        entity.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewedBy)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
