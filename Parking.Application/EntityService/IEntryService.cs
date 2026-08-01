using Parking.Domain.Model.Models;
using System.Threading.Tasks;

namespace Parking.Application.EntityService
{
    public interface IEntryService
    {
        Task<string?> RegisterEntryAsync(string plateNumber, int vehicleTypeId);
        Task<bool> RegisterExitByPlateAsync(string plateNumber);
        Task<bool> RegisterExitByQrAsync(string qrCode);
        Task<parking_session?> GetActiveSessionByPlateAsync(string plateNumber);
        Task<parking_session?> GetActiveSessionByQrAsync(string qrCode);
    }
}