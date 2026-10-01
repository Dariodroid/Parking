using System.Collections.Concurrent;

using Parking.Application.Dto;

namespace Parking.UI.Windows.Services;

/// <summary>Conserva fallos activos de las cámaras durante la sesión de la aplicación.</summary>
public sealed class CameraHealthMonitor
{
    private readonly ConcurrentDictionary<string, (DateTime At, string Detail)> _failures = new();

    /// <summary>Marca una fuente que dejó de entregar vídeo.</summary>
    /// <param name="feed">Nombre del visor.</param>
    /// <param name="detail">Descripción visible sin URL ni credenciales.</param>
    public void ReportFailure(string feed, string detail) => _failures[feed] = (DateTime.Now, detail);

    /// <summary>Retira la alerta cuando vuelve la imagen o el operador detiene la fuente.</summary>
    /// <param name="feed">Nombre del visor.</param>
    public void Clear(string feed) => _failures.TryRemove(feed, out _);

    /// <summary>Devuelve una instantánea de los fallos de vídeo actuales.</summary>
    public IReadOnlyList<ControlIncident> GetIncidents() => _failures.Select(item =>
        new ControlIncident($"camera:{item.Key}", "Cámara", item.Key, item.Value.At,
            item.Value.Detail, 0m, string.Empty)).ToList();
}
