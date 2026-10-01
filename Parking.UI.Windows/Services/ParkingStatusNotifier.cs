using Parking.UI.Windows.Interfaces;
using System;

namespace Parking.UI.Windows.Services;

/// <summary>Difunde cambios de ocupación a las pantallas que presentan el estado del parqueadero.</summary>
public sealed class ParkingStatusNotifier : IParkingStatusNotifier
{
    /// <summary>Se produce después de registrar una entrada o salida que cambia la ocupación.</summary>
    public event EventHandler? ParkingStatusChanged;

    /// <summary>Comunica a las vistas suscritas que deben actualizar su estado de ocupación.</summary>
    public void NotifyParkingStatusChanged()
    {
        // El operador condicional evita invocar el evento cuando no hay suscriptores.
        ParkingStatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
