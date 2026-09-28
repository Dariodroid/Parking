namespace Parking.UI.Windows.Services;

/// <summary>Selecciones de los dos visores guardadas para la siguiente apertura.</summary>
/// <param name="EntranceSourceId">Índice local o marcador de red del visor de entrada.</param>
/// <param name="EntranceUrl">URL de red usada en la entrada, si corresponde.</param>
/// <param name="ExitSourceId">Índice local o marcador de red del visor de salida.</param>
/// <param name="ExitUrl">URL de red usada en la salida, si corresponde.</param>
public sealed record CameraSelectionConfiguration(
    string? EntranceSourceId,
    string EntranceUrl,
    string? ExitSourceId,
    string ExitUrl);
