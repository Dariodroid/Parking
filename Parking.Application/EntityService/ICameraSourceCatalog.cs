namespace Parking.Application.EntityService;

/// <summary>Enumera cámaras conectadas que OpenCV puede abrir en Windows.</summary>
public interface ICameraSourceCatalog
{
    /// <summary>Busca índices de cámara disponibles sin conservarlos abiertos.</summary>
    /// <param name="cancellationToken">Permite cancelar la búsqueda durante el cierre de la vista.</param>
    /// <returns>Fuentes de dispositivo listas para seleccionar.</returns>
    Task<IReadOnlyList<CameraSource>> DiscoverAsync(CancellationToken cancellationToken = default);
}
