using System;

namespace Parking.UI.Windows.Services;

public interface IParkingStatusNotifier
{
    event EventHandler? ParkingStatusChanged;

    void NotifyParkingStatusChanged();
}

public sealed class ParkingStatusNotifier : IParkingStatusNotifier
{
    public event EventHandler? ParkingStatusChanged;

    public void NotifyParkingStatusChanged()
        => ParkingStatusChanged?.Invoke(this, EventArgs.Empty);
}
