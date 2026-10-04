using Parking.Application.Dto;

namespace Parking.UI.Windows.Services;

/// <summary>Redacta los filtros aplicados al informe de vehículos para pantalla y exportación.</summary>
public static class VehicleReportCriteriaFormatter
{
    /// <summary>Describe el rango de fechas aplicado al informe de vehículos.</summary>
    /// <param name="filter">Filtro cuyo período debe imprimirse.</param>
    /// <returns>Texto con fecha inicial y final, o indicación de extremo ausente.</returns>
    public static string VehiclePeriod(VehicleReportFilterDto filter) =>
        $"{filter.FromDate?.ToString("dd/MM/yyyy") ?? "Sin inicio"} al {filter.ToDate?.ToString("dd/MM/yyyy") ?? "Sin fin"}";
    /// <summary>Convierte categorías y estados seleccionados en un texto legible.</summary>
    /// <param name="filter">Filtro aplicado a las filas del informe.</param>
    /// <returns>Resumen textual de placa, propietario, categorías y estados.</returns>
    public static string VehicleCriteria(VehicleReportFilterDto filter) =>
        $"Placa: {Value(filter.Plate)} · Propietario: {Value(filter.OwnerName)} · " +
        $"Categorías: {Choices(filter.IncludeMonthly, filter.IncludeOccasional, "Mensual", "Ocasional")} · " +
        $"Estado: {Choices(filter.IncludeInside, filter.IncludeOutside, "Dentro", "Fuera")}";
    /// <summary>Presenta Todos cuando un criterio de texto está vacío.</summary>
    /// <param name="value">Texto introducido en el filtro.</param>
    /// <returns>Texto recortado o Todos.</returns>
    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "Todos" : value.Trim();
    /// <summary>Resume una pareja de opciones booleanas del filtro.</summary>
    /// <param name="first">Indica si se incluyó la primera opción.</param>
    /// <param name="second">Indica si se incluyó la segunda opción.</param>
    /// <param name="firstName">Nombre visible de la primera opción.</param>
    /// <param name="secondName">Nombre visible de la segunda opción.</param>
    /// <returns>Todos, una opción concreta o Según búsqueda.</returns>
    private static string Choices(bool first, bool second, string firstName, string secondName)
    {
        if (first && second) return "Todos";
        if (first) return firstName;
        if (second) return secondName;
        return "Según búsqueda";
    }
}
