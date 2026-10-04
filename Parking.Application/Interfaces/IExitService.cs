using Parking.Domain.Model.Models;

namespace Parking.Application.Interfaces;

/// <summary>Consulta sesiones abiertas y registra su salida por placa o ticket QR.</summary>
public interface IExitService
{
    /// <summary>Busca la estancia abierta correspondiente a una placa.</summary>
    Task<parking_session?> GetActiveSessionByPlateAsync(string plateNumber);

    /// <summary>Busca la estancia abierta indicada por el ticket QR.</summary>
    Task<parking_session?> GetActiveSessionByQrAsync(string qrCode);

    /// <summary>Cierra la sesión encontrada por placa con el medio de pago elegido.</summary>
    Task<bool> RegisterExitByPlateAsync(string plateNumber, string paymentMethod = "other");

    /// <summary>Cierra la sesión encontrada por QR con el medio de pago elegido.</summary>
    Task<bool> RegisterExitByQrAsync(string qrCode, string paymentMethod = "other");
}
