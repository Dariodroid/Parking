using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess;

/// <summary>Amplía el contexto generado con tablas propias de la aplicación.</summary>
public partial class parking_dbContext
{
    /// <summary>Cuotas mensuales cobradas y conservadas por período.</summary>
    public virtual DbSet<ParkingMonthlyFeeReceipt> MonthlyFeeReceipts => Set<ParkingMonthlyFeeReceipt>();

    /// <summary>Revisiones firmadas del centro de control.</summary>
    public virtual DbSet<ParkingIncidentReview> IncidentReviews => Set<ParkingIncidentReview>();

    /// <summary>Cierres de caja firmados por cada operador.</summary>
    public virtual DbSet<ParkingShiftClosure> ShiftClosures => Set<ParkingShiftClosure>();

    /// <summary>Incorpora las tres entidades al modelo sin editar el archivo regenerable.</summary>
    /// <param name="modelBuilder">Modelo relacional creado por el contexto principal.</param>
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ParkingMonthlyFeeReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new ParkingIncidentReviewConfiguration());
        modelBuilder.ApplyConfiguration(new ParkingShiftClosureConfiguration());
    }
}
