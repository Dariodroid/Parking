using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Parking.Application.Dto;

public class ParkingSlotDashboardItemDTO : INotifyPropertyChanged
{
    private int _positionX;
    private int _positionY;

    public int PositionX
    {
        get => _positionX;
        set
        {
            if (_positionX != value)
            {
                _positionX = value;
                OnPropertyChanged();
            }
        }
    }

    public int PositionY
    {
        get => _positionY;
        set
        {
            if (_positionY != value)
            {
                _positionY = value;
                OnPropertyChanged();
            }
        }
    }

    public int SlotId { get; set; }

    public string SlotNumber { get; set; } = string.Empty;

    public bool IsOccupied { get; set; }

    public string? OwnerName { get; set; }

    public string? Plate { get; set; }

    public string? VehicleType { get; set; }

    public DateTime? EntryTime { get; set; }

    public int SortOrder
    {
        get
        {
            var match = Regex.Match(SlotNumber, @"^\d+");
            return match.Success && int.TryParse(match.Value, out int order)
                ? order : int.MaxValue;
        }
    }

    /// <summary>Notifica a la vista los cambios de posición del puesto.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
