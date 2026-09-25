using System.Threading.Tasks;

namespace Parking.Application.EntityService;

public interface IEntryPhotoStore
{
    Task<string> SaveAsync(byte[] jpeg, string sessionCode);
    void Delete(string path);
}
