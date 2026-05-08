using System.ComponentModel.DataAnnotations.Schema;

namespace Parking.Domain.Model.Models;

[Table("vehicle_types")]
public partial class VehicleType
{
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("icon")]
    public string Icon { get; set; }

    [Column("hourly_rate")]
    public decimal HourlyRate { get; set; }

    [Column("is_active")]
    public bool is_active { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public int? DeletedBy { get; set; }

    public virtual ICollection<ParkingSession> ParkingSessions { get; set; } = new List<ParkingSession>();

    public virtual ICollection<RegisteredVehicle> RegisteredVehicles { get; set; } = new List<RegisteredVehicle>();
}