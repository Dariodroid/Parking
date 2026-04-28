using Parking.Domain.Model.Models;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Abstractions
{
    public interface IParkingSessionRepository : IBaseRepository<ParkingSession>
    {
        // Método para buscar si un vehículo ya está dentro y no ha salido
        Task<ParkingSession> GetActiveSessionByPlateAsync(string plate);
        Task<ParkingSession> GetActiveSessionByQrAsync(string qrCode);
    }
}