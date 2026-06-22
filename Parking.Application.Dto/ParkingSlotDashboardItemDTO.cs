using System.Text.RegularExpressions;

public class ParkingSlotDashboardItemDTO
{
    public int SlotId { get; set; }

    public string SlotNumber { get; set; }

    public bool IsOccupied { get; set; }

    public string? OwnerName { get; set; }

    public string? Plate { get; set; }

    public string? VehicleType { get; set; }

    public DateTime? EntryTime { get; set; }

    public int SortOrder
    {
        get
        {
            var match =
                Regex.Match(
                    SlotNumber ?? "",
                    @"^\d+");

            if (match.Success)
            {
                return int.Parse(
                    match.Value);
            }

            return int.MaxValue;
        }
    }
}