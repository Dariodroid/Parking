using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.Ports
{
    /// <summary>Puerto de reconocimiento de placas para las imágenes capturadas por la operación.</summary>
    public interface IPlateService
    {
        /// <summary>Extrae una placa de la imagen recibida.</summary>
        /// <param name="image">Fotograma codificado en bytes.</param>
        /// <returns>Placa reconocida o texto vacío si no se encontró una.</returns>
        Task<string> DetectPlateAsync(byte[] image);
    }
}
