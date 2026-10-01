using Parking.Domain.Model.Models;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Interfaces
{
    public interface Iparking_sessionRepository : IBaseRepository<parking_session>
    {
        /// <summary>Busca la estancia abierta de una placa.</summary>
        /// <param name="plate">Placa normalizada del vehículo.</param>
        /// <returns>Sesión activa o nulo cuando no hay una entrada pendiente de salida.</returns>
        Task<parking_session?> GetActiveSessionByPlateAsync(string plate);

        /// <summary>Busca la estancia abierta asociada a un ticket QR.</summary>
        /// <param name="qrCode">Contenido leído del ticket.</param>
        /// <returns>Sesión activa o nulo si el QR no corresponde a una entrada abierta.</returns>
        Task<parking_session?> GetActiveSessionByQrAsync(string qrCode);
    }
}
