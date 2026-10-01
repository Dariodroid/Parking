using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.Interfaces
{
    /// <summary>Puerto de lectura de QR para localizar tickets de salida.</summary>
    public interface IQrService
    {
        /// <summary>Lee el contenido QR de un fotograma.</summary>
        /// <param name="imageFrame">Fotograma codificado en bytes.</param>
        /// <returns>Contenido leído o texto vacío si no se detectó un QR.</returns>
        Task<string> ReadQrAsync(byte[] imageFrame);
    }
}
