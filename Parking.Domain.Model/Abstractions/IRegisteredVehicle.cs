using Parking.Domain.Model.Models;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Abstractions
{
    public interface IRegisteredVehicle : IBaseRepository<registered_vehicle>
    {
        Task<bool> ExistsByPlateAsync(string plate);

        Task SoftDeleteAsync(registered_vehicle entity, int deletedBy);
    }
}