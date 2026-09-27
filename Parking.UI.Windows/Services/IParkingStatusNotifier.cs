namespace Parking.UI.Windows.Services;

/// <summary>Contrato para avisar a las vistas cuando cambia la ocupación.</summary>
public interface IParkingStatusNotifier
{
    /// <summary>Notificación a quienes presentan disponibilidad o estadísticas.</summary>
    event EventHandler? ParkingStatusChanged;

    /// <summary>Emite la notificación tras confirmar una entrada o salida.</summary>
    void NotifyParkingStatusChanged();
}
