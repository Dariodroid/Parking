using Parking.Application.EntityService;
using Parking.Application.Services;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using System;
using System.Threading.Tasks;

namespace Parking.Application.UseCases
{
    /// <summary>
    /// Orquesta entradas y salidas, conservando en cada sesión si el ingreso
    /// tuvo acceso mensual o debe cobrarse como ocasional.
    /// </summary>
    public class EntryService : IEntryService
    {
        // Las sesiones anteriores usaban un código hexadecimal de 8 caracteres
        // y guardaban fechas UTC sin zona. Este prefijo identifica las nuevas
        // sesiones con horas locales sin modificar los registros históricos.
        private const string LocalTimeSessionPrefix = "EC-";
        private readonly Iparking_sessionRepository _sessionRepo;
        private readonly IParkingSlotRepository _slotRepo;
        private readonly IEntryPhotoStore _photoStore;
        private readonly IQrTicketStore _qrTicketStore;
        // Permiten consultar contrato/horarios y la tarifa del tipo de vehículo.
        private readonly IRegisteredVehicle _registeredVehicles;
        private readonly Ivehicle_typeRepository _vehicleTypes;
        private readonly IBaseRepository<payment> _paymentRepo;

        /// <summary>Recibe los repositorios y almacenes usados al abrir y cerrar sesiones.</summary>
        /// <param name="sessionRepo">Consulta y persiste las sesiones de estacionamiento.</param>
        /// <param name="slotRepo">Localiza puestos libres y actualiza su ocupación.</param>
        /// <param name="photoStore">Guarda o elimina la fotografía opcional de la placa.</param>
        /// <param name="qrTicketStore">Genera o elimina el QR de entradas ocasionales.</param>
        /// <param name="registeredVehicles">Busca clientes con plan y horarios completos.</param>
        /// <param name="vehicleTypes">Consulta la tarifa por hora del tipo de vehículo.</param>
        /// <param name="paymentRepo">Agrega el pago al mismo contexto de la sesión y el puesto.</param>
        public EntryService(
            Iparking_sessionRepository sessionRepo,
            IParkingSlotRepository slotRepo,
            IEntryPhotoStore photoStore,
            IQrTicketStore qrTicketStore,
            IRegisteredVehicle registeredVehicles,
            Ivehicle_typeRepository vehicleTypes,
            IBaseRepository<payment> paymentRepo)
        {
            // Se conservan las dependencias para usarlas en toda la operación.
            _sessionRepo = sessionRepo;
            _slotRepo = slotRepo;
            _photoStore = photoStore;
            _qrTicketStore = qrTicketStore;
            _registeredVehicles = registeredVehicles;
            _vehicleTypes = vehicleTypes;
            _paymentRepo = paymentRepo;
        }

        /// <summary>Obtiene la estancia abierta de una placa normalizada.</summary>
        /// <param name="plateNumber">Placa escrita o detectada por la cámara.</param>
        /// <returns>Sesión activa o nulo si el vehículo está fuera.</returns>
        public async Task<parking_session?> GetActiveSessionByPlateAsync(string plateNumber)
        {
            // La persistencia usa mayúsculas para que la consulta coincida con la entrada.
            return await _sessionRepo.GetActiveSessionByPlateAsync(plateNumber.Trim().ToUpperInvariant());
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
                var availableSlot = selectedSlotId.HasValue
                    ? await _slotRepo.GetAvailableSlotByIdAsync(selectedSlotId.Value)
                    : await _slotRepo.GetFirstAvailableSlotAsync();

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
                var session = new parking_session
                {
                    // La placa y el código interno identifican la estancia.
                    plate = normalized,
                    session_code = LocalTimeSessionPrefix + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
                    // Los clientes mensuales se identifican por placa; únicamente
                    // las entradas con tarifa ocasional necesitan ticket QR.
                    qr_data = access.IsMonthly ? null : $"SESSION-{Guid.NewGuid():N}".ToUpper(),
                    // Se guarda la hora local que sirvió para evaluar el acceso.
                    entry_time = entryTime,
                    status = "active",
                    // La ficha del cliente sigue vinculada incluso si paga como ocasional.
                    vehicle_type_id = resolvedVehicleTypeId,
                    registered_vehicle_id = registered?.id,
                    // Se conserva la modalidad y, si corresponde tarifa ocasional a un cliente,
                    // el motivo exacto evaluado al entrar para permitir auditorías posteriores.
                    notes = access.IsMonthly ? MonthlyAccessPolicy.MonthlySessionNote
                        : registered is not null
                            ? ParkingSessionNotes.OccasionalReasonPrefix + access.Kind
                            : null,
                    // Se atribuye la apertura al operador actual.
                    entry_operator_id = CurrentUser.Id,
                    created_by = CurrentUser.Id,
                    created_at = entryTime,
                    is_deleted = false,

                    // La sesión conserva el puesto aunque luego se libere.
                    parking_slot_id = availableSlot.id
                };

                // Se guarda evidencia fotográfica únicamente si hay captura.
                if (plateImage?.Length > 0)
                {
                    photoPath = await _photoStore.SaveAsync(plateImage, session.session_code);
                    session.entry_photo_path = photoPath;
                }

                // El mensualizado no tiene qr_data y por eso no se crea archivo QR.
                if (session.qr_data is { } ticketQr)
                    qrPath = await _qrTicketStore.SaveAsync(ticketQr, session.session_code);

                // Se agrega la sesión al mismo contexto usado para el puesto.
                await _sessionRepo.AddAsync(session);

                // El puesto queda ocupado y enlazado a la nueva sesión.
                availableSlot.is_occupied = true;
                availableSlot.current_session = session;
                availableSlot.updated_at = entryTime;

                await _slotRepo.UpdateAsync(availableSlot);

                // Una sola confirmación persiste sesión y ocupación del puesto.
                bool success = await _sessionRepo.SaveChangesAsync();

                if (!success)
                    throw new InvalidOperationException("La base de datos no confirmó el registro de la entrada.");

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

        /// <summary>Cierra la sesión encontrada por placa; permite salir sin ticket al mensualizado.</summary>
        /// <param name="plateNumber">Placa leída o introducida al salir.</param>
        /// <param name="paymentMethod">Medio elegido por el operador.</param>
        /// <returns>Verdadero cuando el cierre y la liberación del puesto quedan guardados.</returns>
        public async Task<bool> RegisterExitByPlateAsync(string plateNumber, string paymentMethod = "other")
        {
            // La búsqueda usa el mismo formato normalizado que el ingreso.
            var session = await _sessionRepo.GetActiveSessionByPlateAsync(plateNumber.Trim().ToUpperInvariant());
            // Ambas vías de salida comparten la misma regla de cobro.
            return await FinalizeSession(session, paymentMethod);
        }

        /// <summary>Cierra una sesión ocasional localizada con el identificador del ticket QR.</summary>
        /// <param name="qrCode">Contenido SESSION leído del QR.</param>
        /// <param name="paymentMethod">Medio elegido por el operador.</param>
        /// <returns>Verdadero cuando el cierre y la liberación del puesto quedan guardados.</returns>
        public async Task<bool> RegisterExitByQrAsync(string qrCode, string paymentMethod = "other")
        {
            // Un cliente mensual nuevo no tiene qr_data y sale mediante su placa.
            var session = await _sessionRepo.GetActiveSessionByQrAsync(qrCode.Trim());
            return await FinalizeSession(session, paymentMethod);
        }

        /// <summary>Calcula el importe según la modalidad fijada al entrar y libera el puesto.</summary>
        /// <param name="session">Sesión abierta hallada por placa o QR; puede ser nula.</param>
        /// <param name="paymentMethod">Medio informado para el pago ocasional.</param>
        /// <returns>Falso si no hay sesión o si la base no confirmó el guardado.</returns>
        private async Task<bool> FinalizeSession(parking_session? session, string paymentMethod)
        {
            // No hay salida que registrar si la búsqueda no encontró sesión.
            if (session == null) return false;

            // Las sesiones recientes usan hora local; las antiguas se convierten abajo.
            DateTime exitTime = DateTime.Now;
            DateTime entryTime = session.entry_time;
            if (!session.session_code.StartsWith(LocalTimeSessionPrefix, StringComparison.Ordinal))
            {
                // Sesión abierta antes del cambio: su hora de entrada era UTC.
                // Convertimos la fila al horario local al cerrarla y calculamos
                // la duración con los instantes UTC originales.
                DateTime entryUtc = DateTime.SpecifyKind(entryTime, DateTimeKind.Utc);
                entryTime = entryUtc.ToLocalTime();
                session.entry_time = entryTime;
                session.created_at = DateTime.SpecifyKind(session.created_at, DateTimeKind.Utc).ToLocalTime();
            }

            // Se conserva la hora real de salida y la duración total de estancia.
            session.exit_time = exitTime;
            TimeSpan duration = exitTime - entryTime;

            // La modalidad quedó fijada al registrar la entrada. Cambios
            // posteriores al plan u horario no alteran el cobro de esta sesión.
            // Para ocasionales se conserva la regla previa de hora iniciada:
            // cualquier fracción de hora cuenta como una hora, con mínimo de una.
            decimal hoursToCharge = (decimal)Math.Ceiling(duration.TotalHours);
            if (hoursToCharge < 1) hoursToCharge = 1;

            // duration_minutes refleja el tiempo real; chargeable_minutes, el cobrado.
            session.duration_minutes = (int)duration.TotalMinutes;
            // La marca persistida al entrar evita recalcular fechas u horarios al salir.
            bool monthlyAccess = session.notes == MonthlyAccessPolicy.MonthlySessionNote;
            session.chargeable_minutes = monthlyAccess
                ? 0 : (int)(hoursToCharge * 60);
            // Una sesión mensual vigente no genera importe por la estancia.
            if (monthlyAccess)
                session.amount_due = 0;
            else
            {
                // Se obtiene la tarifa configurada para el tipo de esta sesión.
                var vehicleType = await _vehicleTypes.GetByIdAsync(session.vehicle_type_id);
                if (vehicleType == null || vehicleType.is_deleted)
                    throw new InvalidOperationException("No se encontró la tarifa del tipo de vehículo.");
                // El importe ocasional resulta de horas iniciadas por tarifa/hora.
                // La tarifa y el producto deben caber exactamente en decimal(10,2).
                // Rechazar un dato inválido evita que SQL redondee o desborde al guardar.
                decimal hourlyRate = MoneyAmount.RequireValid(vehicleType.hourly_rate, "La tarifa por hora");
                session.amount_due = MoneyAmount.RequireValid(hoursToCharge * hourlyRate, "El cobro de salida");
            }

            // El cierre pagado y el asiento contable se confirman juntos.
            session.status = "paid";
            // La fecha y el operador identifican quién efectuó el cierre.
            session.updated_at = exitTime;
            session.exit_operator_id = CurrentUser.Id;

            // EF registra los cambios en la sesión antes de guardar el contexto.
            await _sessionRepo.UpdateAsync(session);

            if (session.amount_due is > 0)
            {
                // Si el operador no indicó un medio, se registra como desconocido
                // en lugar de atribuir el dinero incorrectamente a efectivo.
                string method = paymentMethod?.Trim().ToLowerInvariant() switch
                {
                    "cash" => "cash",
                    "transfer" => "transfer",
                    "card" => "card",
                    _ => "other"
                };
                await _paymentRepo.AddAsync(new payment
                {
                    session_id = session.id,
                    amount_paid = session.amount_due.Value,
                    payment_method = method,
                    notes = method == "other"
                        ? "Salida registrada sin indicar el medio de pago."
                        : "Cobro registrado al cerrar la sesión.",
                    collected_by = CurrentUser.Id,
                    collected_at = exitTime,
                    created_at = exitTime,
                    is_deleted = false
                });
            }

            // 5. LIBERAR EL PUESTO DE ESTACIONAMIENTO
            // La sesión conserva su parking_slot_id aunque current_session_id no se haya sincronizado.
            // Usar ese id garantiza que una salida QR libere el puesto correcto.
            var occupiedSlot = session.parking_slot_id.HasValue
                ? await _slotRepo.GetByIdAsync(session.parking_slot_id.Value)
                : null;

            if (occupiedSlot != null)
            {
                // Se libera el puesto y se retira el enlace a la sesión actual;
                // parking_slot_id permanece en la sesión como dato histórico.
                occupiedSlot.is_occupied = false;
                occupiedSlot.current_session_id = null;
                occupiedSlot.current_session = null;
                occupiedSlot.updated_at = exitTime;
                await _slotRepo.UpdateAsync(occupiedSlot);
            }

            // Un único SaveChanges persiste sesión, pago y puesto en la misma transacción de EF.
            return await _sessionRepo.SaveChangesAsync();
        }

        /// <summary>Consulta una sesión abierta por el valor codificado en el ticket.</summary>
        /// <param name="qrCode">Texto leído del QR ocasional.</param>
        /// <returns>Sesión abierta o nulo si no existe.</returns>
        public async Task<parking_session?> GetActiveSessionByQrAsync(string qrCode)
        {
            // Se eliminan espacios accidentales alrededor del dato escaneado.
            return await _sessionRepo.GetActiveSessionByQrAsync(qrCode.Trim());
        }
    }
}
