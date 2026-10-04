using Parking.Application.Interfaces;
using Parking.Application.UseCases;
using Parking.Domain.Model.Interfaces;
using Parking.Domain.Model.Models;
using Parking.Domain.Model.Policies;
using System;
using System.Threading.Tasks;

namespace Parking.Application.Services
{
    /// <summary>
    /// Registra entradas y conserva en cada sesión si el acceso fue mensual
    /// o debe cobrarse como ocasional al salir.
    /// </summary>
    public class EntryService : IEntryService
    {
        private readonly Iparking_sessionRepository _sessionRepo;
        private readonly IParkingSlotRepository _slotRepo;
        private readonly IEntryPhotoStore _photoStore;
        private readonly IQrTicketStore _qrTicketStore;
        // Permite consultar el contrato y horario del cliente.
        private readonly IRegisteredVehicle _registeredVehicles;

        /// <summary>Recibe los repositorios y almacenes usados al registrar entradas.</summary>
        /// <param name="sessionRepo">Consulta y persiste las sesiones de estacionamiento.</param>
        /// <param name="slotRepo">Localiza puestos libres y actualiza su ocupación.</param>
        /// <param name="photoStore">Guarda o elimina la fotografía opcional de la placa.</param>
        /// <param name="qrTicketStore">Genera o elimina el QR de entradas ocasionales.</param>
        /// <param name="registeredVehicles">Busca clientes con plan y horarios completos.</param>
        public EntryService(
            Iparking_sessionRepository sessionRepo,
            IParkingSlotRepository slotRepo,
            IEntryPhotoStore photoStore,
            IQrTicketStore qrTicketStore,
            IRegisteredVehicle registeredVehicles)
        {
            // Se conservan las dependencias para usarlas en toda la operación.
            _sessionRepo = sessionRepo;
            _slotRepo = slotRepo;
            _photoStore = photoStore;
            _qrTicketStore = qrTicketStore;
            _registeredVehicles = registeredVehicles;
        }

        /// <summary>
        /// Conserva la variante anterior que solo devuelve el puesto; la decisión
        /// mensual se calcula en <see cref="RegisterEntryDetailedAsync"/>.
        /// </summary>
        /// <param name="plateNumber">Placa que se registra.</param>
        /// <param name="vehicleTypeId">Tipo elegido para una placa ocasional.</param>
        /// <param name="plateImage">Imagen opcional de la placa.</param>
        /// <param name="selectedSlotId">Puesto elegido o nulo para asignación automática.</param>
        /// <returns>Puesto, EXISTENTE o nulo; la información de acceso se descarta en esta variante.</returns>
        public async Task<string?> RegisterEntryAsync(string plateNumber, int vehicleTypeId, byte[]? plateImage = null, int? selectedSlotId = null)
            => (await RegisterEntryDetailedAsync(plateNumber, vehicleTypeId, plateImage, selectedSlotId)).SlotNumber;

        /// <summary>Clasifica la placa, abre una sesión y crea ticket solo cuando corresponde tarifa ocasional.</summary>
        /// <param name="plateNumber">Placa del vehículo que ingresa.</param>
        /// <param name="vehicleTypeId">Tipo seleccionado; se reemplaza por el tipo guardado si es cliente registrado.</param>
        /// <param name="plateImage">Foto opcional que se vincula a la sesión.</param>
        /// <param name="selectedSlotId">Puesto preferido; nulo selecciona el primero libre.</param>
        /// <returns>Puesto y clasificación, o un resultado que indica sesión existente o falta de cupo.</returns>
        public async Task<EntryRegistrationResult> RegisterEntryDetailedAsync(string plateNumber, int vehicleTypeId, byte[]? plateImage = null, int? selectedSlotId = null)
        {
            // No se crea una sesión para una placa vacía.
            if (string.IsNullOrWhiteSpace(plateNumber))
                return new(null, new(MonthlyAccessKind.Occasional, 0));

            // Las rutas permiten borrar archivos creados si la operación falla.
            string? photoPath = null;
            string? qrPath = null;
            try
            {
                // Todas las consultas y sesiones usan el mismo formato de placa.
                string normalized = plateNumber.Trim().ToUpperInvariant();

                // Evita ocupar otro puesto mientras la misma placa siga dentro.
                var activeSession = await _sessionRepo.GetActiveSessionByPlateAsync(normalized);
                if (activeSession != null)
                    return new("EXISTENTE", new(MonthlyAccessKind.Occasional, 0));

                // La fecha se toma una sola vez para evaluar contrato y registrar entrada.
                DateTime entryTime = DateTime.Now;
                // La consulta trae plan y horarios; un cliente inactivo también se evalúa.
                var registered = await _registeredVehicles.GetCompleteByPlateAsync(normalized);
                // Se decide una sola vez si el ingreso tendrá beneficio mensual.
                var access = MonthlyAccessPolicy.Evaluate(registered, entryTime);
                // El tipo de un cliente proviene de su ficha, aunque pague como ocasional.
                int resolvedVehicleTypeId = registered?.vehicle_type_id ?? vehicleTypeId;
                // Una placa ocasional necesita un tipo para calcular su tarifa.
                if (resolvedVehicleTypeId <= 0)
                    throw new InvalidOperationException("Seleccione un tipo de vehículo para la entrada ocasional.");

                // Ambas consultas devuelven la entidad seguida por EF. El puesto
                // elegido se valida otra vez al guardar por si dejó de estar libre.
                var availableSlot = await FindAvailableSlotAsync(selectedSlotId);

                if (availableSlot == null)
                {
                    // Si el puesto elegido se ocupó, el operador debe escoger otro.
                    if (selectedSlotId.HasValue)
                        throw new InvalidOperationException("El puesto seleccionado ya no está libre. Elige otro.");
                    // Sin puesto elegido y sin cupo, se informa sin abrir sesión.
                    return new(null, access); // Parqueadero lleno
                }

                // Se crea una sesión para todo vehículo, también para el mensualizado
                // que no recibe ticket: así se registran su entrada y posterior salida.
                var session = CreateSession(normalized, resolvedVehicleTypeId, registered,
                    availableSlot, access, entryTime);

                // Se guarda evidencia fotográfica únicamente si hay captura.
                if (plateImage?.Length > 0)
                {
                    photoPath = await _photoStore.SaveAsync(plateImage, session.session_code);
                    session.entry_photo_path = photoPath;
                }

                // El mensualizado no tiene qr_data y por eso no se crea archivo QR.
                if (session.qr_data != null)
                    qrPath = await _qrTicketStore.SaveAsync(session.qr_data, session.session_code);

                await SaveEntryAsync(session, availableSlot, entryTime);

                // El ticket se expone únicamente después de confirmar la sesión y el puesto.
                EntryTicketData? ticket = qrPath is null ? null : new EntryTicketData(
                    session.session_code, session.plate, availableSlot.slot_number, entryTime, qrPath);
                // La UI recibe los datos de impresión sin volver a buscar una sesión mutable.
                return new(availableSlot.slot_number, access, ticket);
            }
            catch
            {
                // Si falló la persistencia, se eliminan los archivos externos
                // creados antes de propagar el error al operador.
                if (photoPath != null) _photoStore.Delete(photoPath);
                if (qrPath != null) _qrTicketStore.Delete(qrPath);
                throw;
            }
        }

        /// <summary>Busca el puesto elegido o el primero disponible, sin ocuparlo todavía.</summary>
        private Task<parking_slot?> FindAvailableSlotAsync(int? selectedSlotId)
        {
            if (selectedSlotId.HasValue)
                return _slotRepo.GetAvailableSlotByIdAsync(selectedSlotId.Value);

            return _slotRepo.GetFirstAvailableSlotAsync();
        }

        /// <summary>Construye la sesión con la modalidad fijada al ingresar.</summary>
        private static parking_session CreateSession(string plate, int vehicleTypeId,
            registered_vehicle? registered, parking_slot slot, MonthlyAccessDecision access, DateTime entryTime)
        {
            string? notes = null;
            if (access.IsMonthly)
                notes = MonthlyAccessPolicy.MonthlySessionNote;
            else if (registered != null)
                notes = ParkingSessionNotes.OccasionalReasonPrefix + access.Kind;

            return new parking_session
            {
                plate = plate,
                session_code = ParkingSessionTime.LocalSessionPrefix + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
                qr_data = access.IsMonthly ? null : $"SESSION-{Guid.NewGuid():N}".ToUpper(),
                entry_time = entryTime,
                status = "active",
                vehicle_type_id = vehicleTypeId,
                registered_vehicle_id = registered?.id,
                notes = notes,
                entry_operator_id = CurrentUser.Id,
                created_by = CurrentUser.Id,
                created_at = entryTime,
                is_deleted = false,
                parking_slot_id = slot.id
            };
        }

        /// <summary>Guarda sesión y ocupación juntas en el contexto compartido por los repositorios.</summary>
        private async Task SaveEntryAsync(parking_session session, parking_slot slot, DateTime entryTime)
        {
            await _sessionRepo.AddAsync(session);
            slot.is_occupied = true;
            slot.current_session = session;
            slot.updated_at = entryTime;
            await _slotRepo.UpdateAsync(slot);
            if (!await _sessionRepo.SaveChangesAsync())
                throw new InvalidOperationException("La base de datos no confirmó el registro de la entrada.");
        }

    }
}
