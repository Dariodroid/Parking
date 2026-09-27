using System.Threading.Tasks;

namespace Parking.Application.EntityService;

public interface IQrTicketStore
{
    Task<string> SaveAsync(string qrData, string sessionCode);
    void Delete(string path);
}
