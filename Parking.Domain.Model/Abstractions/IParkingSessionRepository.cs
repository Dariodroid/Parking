using Parking.Domain.Model.Models;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Abstractions
{
    public interface Iparking_sessionRepository : IBaseRepository<parking_session>
    {
        // Método para buscar si un vehículo ya está dentro y no ha salido
        Task<parking_session> GetActiveSessionByPlateAsync(string plate);
        Task<parking_session> GetActiveSessionByQrAsync(string qrCode);
    }
}