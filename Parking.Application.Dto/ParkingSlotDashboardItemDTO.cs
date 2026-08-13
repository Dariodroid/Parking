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
            var match = Regex.Match(SlotNumber ?? "", @"^\d+");
            if (match.Success)
            {
                return int.Parse(match.Value);
            }
            return int.MaxValue;
        }
    }

    // 🟢 Evento de notificación de cambios
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}