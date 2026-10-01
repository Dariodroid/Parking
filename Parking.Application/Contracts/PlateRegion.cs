namespace Parking.Application.Contracts;

/// <summary>Coordenadas de una región de placa en píxeles, independientes de OpenCV.</summary>
/// <param name="X">Distancia desde el borde izquierdo del fotograma.</param>
/// <param name="Y">Distancia desde el borde superior del fotograma.</param>
/// <param name="Width">Anchura de la región.</param>
/// <param name="Height">Altura de la región.</param>
public readonly record struct PlateRegion(int X, int Y, int Width, int Height);
