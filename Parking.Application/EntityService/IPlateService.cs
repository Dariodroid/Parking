using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.EntityService
{
    public interface IPlateService
    {
        Task<string> DetectPlateAsync(byte[] image);
        //Task<string> RecognizePlateAsync(byte[] imageFrame);
    }
}
