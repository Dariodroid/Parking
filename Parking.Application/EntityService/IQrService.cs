using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.EntityService
{
    public interface IQrService
    {
        // Recibe un array de bytes y devuelve el código QR desencriptado/leído
        Task<string> ReadQrAsync(byte[] imageFrame);
    }
}
