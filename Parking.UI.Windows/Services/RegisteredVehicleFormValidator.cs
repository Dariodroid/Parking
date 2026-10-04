using Parking.Application.UseCases;

namespace Parking.UI.Windows.Services;

/// <summary>Valida los datos editados de una ficha mensual antes de enviarlos a Application.</summary>
public static class RegisteredVehicleFormValidator
{
    /// <summary>Devuelve el primer error de la ficha, o nulo cuando puede guardarse.</summary>
    public static string? GetError(string? plate, int vehicleTypeId, decimal? monthlyFee,
        DateTime? startDate, DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(plate)) return "Ingrese la placa.";
        if (vehicleTypeId <= 0) return "Seleccione el tipo de vehículo.";
        if (monthlyFee is null or <= 0) return "Ingrese el valor mensual.";
        if (!MoneyAmount.IsValid(monthlyFee.Value))
            return "La mensualidad debe tener como máximo dos decimales y caber en la base de datos. No se redondeará automáticamente.";
        if (startDate is null) return "Seleccione fecha inicial.";
        if (endDate is null) return "Seleccione fecha final.";
        if (endDate < startDate) return "La fecha final no puede ser menor.";
        return null;
    }
}
