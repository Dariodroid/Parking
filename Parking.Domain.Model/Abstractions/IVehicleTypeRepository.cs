using Parking.Domain.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Abstractions
{
    public interface Ivehicle_typeRepository : IBaseRepository<vehicle_type>
    {
        Task SoftDeleteAsync(vehicle_type entity);
    }
}
