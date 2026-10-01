using Parking.Application.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Application.Interfaces
{
    /// <summary>Puerto de reconocimiento de placas para las imágenes capturadas por la operación.</summary>
    public interface IPlateService
    {
        /// <summary>Extrae una placa de la imagen recibida.</summary>
        /// <param name="image">Fotograma codificado en bytes.</param>
        /// <returns>Placa reconocida o texto vacío si no se encontró una.</returns>
        Task<string> DetectPlateAsync(byte[] image);

        /// <summary>Reconoce la placa y devuelve la región y el recorte que necesita el visor.</summary>
        /// <param name="image">Fotograma codificado en bytes.</param>
        /// <returns>Lectura y región en coordenadas simples, sin tipos del detector.</returns>
        Task<PlateDetectionResult> DetectPlateWithRegionsAsync(byte[] image);

        /// <summary>Calcula la guía visible de reconocimiento para un fotograma.</summary>
        /// <param name="width">Anchura del fotograma en píxeles.</param>
        /// <param name="height">Altura del fotograma en píxeles.</param>
        /// <returns>Coordenadas de la guía que usa el detector.</returns>
        PlateRegion GetRecognitionRegionCoordinates(int width, int height);
    }
}
