using Parking.Domain.Model.Models;
using System.Threading.Tasks;
using Parking.Application.UseCases;

namespace Parking.Application.EntityService
{
    /// <summary>Contrato para registrar entradas, consultar sesiones abiertas y cerrar salidas.</summary>
    public interface IEntryService
    {
        /// <summary>
        /// Variante compatible que devuelve solo el puesto y delega la clasificación
        /// mensual en <see cref="RegisterEntryDetailedAsync"/>.
        /// </summary>
        /// <param name="plateNumber">Placa detectada o escrita por el operador.</param>
        /// <param name="vehicleTypeId">Tipo elegido; se usa para ocasionales y se reemplaza por el registrado si la placa es cliente.</param>
        /// <param name="plateImage">Captura opcional de la placa que se guarda con la sesión.</param>
        /// <param name="selectedSlotId">Puesto opcional elegido; si falta se asigna el primero libre.</param>
        /// <returns>Número del puesto, EXISTENTE si ya hay sesión activa o nulo si no hay cupo.</returns>
        Task<string?> RegisterEntryAsync(string plateNumber, int vehicleTypeId, byte[]? plateImage = null, int? selectedSlotId = null);

        /// <summary>Registra la entrada y devuelve además el motivo del acceso y la cuota pendiente.</summary>
        /// <param name="plateNumber">Placa que se busca en los clientes registrados.</param>
        /// <param name="vehicleTypeId">Tipo seleccionado para una placa ocasional; un cliente usa su tipo registrado.</param>
        /// <param name="plateImage">Imagen opcional para conservar evidencia de la entrada.</param>
        /// <param name="selectedSlotId">Identificador opcional del puesto; nulo permite asignación automática.</param>
        /// <returns>Puesto asignado y decisión mensual u ocasional; indica falta de cupo o sesión existente.</returns>
        Task<EntryRegistrationResult> RegisterEntryDetailedAsync(string plateNumber, int vehicleTypeId, byte[]? plateImage = null, int? selectedSlotId = null);

        /// <summary>Busca la sesión abierta por placa y registra su salida.</summary>
        /// <param name="plateNumber">Placa cuya estancia debe cerrarse.</param>
        /// <param name="paymentMethod">Medio de pago elegido; sin especificar si no se indicó.</param>
        /// <returns>Verdadero si se guardó el cierre de la sesión.</returns>
        Task<bool> RegisterExitByPlateAsync(string plateNumber, string paymentMethod = "other");

        /// <summary>Busca una sesión ocasional abierta mediante el contenido de su ticket QR.</summary>
        /// <param name="qrCode">Identificador SESSION codificado en el ticket.</param>
        /// <param name="paymentMethod">Medio de pago elegido; sin especificar si no se indicó.</param>
        /// <returns>Verdadero si se guardó el cierre de la sesión.</returns>
        Task<bool> RegisterExitByQrAsync(string qrCode, string paymentMethod = "other");

        /// <summary>Consulta una estancia aún abierta mediante su placa.</summary>
        /// <param name="plateNumber">Placa del vehículo.</param>
        /// <returns>Sesión abierta o nulo si no existe.</returns>
        Task<parking_session?> GetActiveSessionByPlateAsync(string plateNumber);

        /// <summary>Consulta una estancia aún abierta mediante el identificador QR.</summary>
        /// <param name="qrCode">Contenido leído del ticket.</param>
        /// <returns>Sesión abierta o nulo si el QR no corresponde a una.</returns>
        Task<parking_session?> GetActiveSessionByQrAsync(string qrCode);
    }
}
