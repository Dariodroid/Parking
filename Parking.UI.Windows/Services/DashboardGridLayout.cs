using Parking.Application.Dto;

namespace Parking.UI.Windows.Services;

/// <summary>Calcula la posición inicial y el tamaño del mapa de puestos.</summary>
public static class DashboardGridLayout
{
    private const int CardWidth = 160;
    private const int CardHeight = 100;
    private const int HorizontalSpacing = 20;
    private const int VerticalSpacing = 20;
    private const int StartX = 20;
    private const int StartY = 20;
    private const int ContainerWidth = 1000;
    private const int Padding = 40;

    /// <summary>Distribuye los puestos por filas de izquierda a derecha.</summary>
    public static List<(int X, int Y)> CreatePositions(int count)
    {
        int cardsPerRow = Math.Max(1, (ContainerWidth - StartX) / (CardWidth + HorizontalSpacing));
        var positions = new List<(int X, int Y)>(count);
        for (int index = 0; index < count; index++)
        {
            int row = index / cardsPerRow;
            int column = index % cardsPerRow;
            positions.Add((StartX + column * (CardWidth + HorizontalSpacing),
                StartY + row * (CardHeight + VerticalSpacing)));
        }
        return positions;
    }

    /// <summary>Deja espacio después de la tarjeta situada más a la derecha y abajo.</summary>
    public static (double Width, double Height) GetCanvasSize(
        IEnumerable<ParkingSlotDashboardItemDTO> slots)
    {
        var items = slots.ToList();
        if (items.Count == 0) return (0, 0);
        return (items.Max(item => item.PositionX) + CardWidth + Padding,
            items.Max(item => item.PositionY) + CardHeight + Padding);
    }
}
