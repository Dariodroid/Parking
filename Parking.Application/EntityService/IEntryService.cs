using Parking.Domain.Model.Models;

namespace Parking.Application.EntityService
{
    public interface IEntryService
    {
        Task<bool> RegisterEntryAsync(string plateNumber);
        Task<bool> RegisterExitByPlateAsync(string plateNumber);
        Task<bool> RegisterExitByQrAsync(string qrCode);
        Task<parking_session?> GetActiveSessionByPlateAsync(string plateNumber);
        Task<parking_session?> GetActiveSessionByQrAsync(string qrCode);
    }
}