using Parking.Application.EntityService;
using Parking.Application.Services;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using System;
using System.Threading.Tasks;

namespace Parking.Application.UseCases
{
    public class EntryService : IEntryService
    {
        // Las sesiones anteriores usaban un código hexadecimal de 8 caracteres
        // y guardaban fechas UTC sin zona. Este prefijo identifica las nuevas
        // sesiones con horas locales sin modificar los registros históricos.
        private const string LocalTimeSessionPrefix = "EC-";
        private readonly Iparking_sessionRepository _sessionRepo;
        private readonly IParkingSlotRepository _slotRepo;
        private readonly IEntryPhotoStore _photoStore;

        public EntryService(
            Iparking_sessionRepository sessionRepo,
            IParkingSlotRepository slotRepo,
            IEntryPhotoStore photoStore)
        {
            _sessionRepo = sessionRepo;
            _slotRepo = slotRepo;
            _photoStore = photoStore;
        }

        public async Task<parking_session?> GetActiveSessionByPlateAsync(string plateNumber)
        {
            return await _sessionRepo.GetActiveSessionByPlateAsync(plateNumber.Trim().ToUpperInvariant());
        }

        public async Task<string?> RegisterEntryAsync(string plateNumber, int vehicleTypeId, byte[]? plateImage = null, int? selectedSlotId = null)
        {
            if (string.IsNullOrWhiteSpace(plateNumber))
                return null;

            string? photoPath = null;
            try
            {
                string normalized = plateNumber.Trim().ToUpperInvariant();

                var activeSession = await _sessionRepo.GetActiveSessionByPlateAsync(normalized);
                if (activeSession != null)
                    return "EXISTENTE";

                // Ambas consultas devuelven la entidad seguida por EF. El puesto
                // elegido se valida otra vez al guardar por si dejó de estar libre.
                var availableSlot = selectedSlotId.HasValue
                    ? await _slotRepo.GetAvailableSlotByIdAsync(selectedSlotId.Value)
                    : await _slotRepo.GetFirstAvailableSlotAsync();

                if (availableSlot == null)
                {
                    if (selectedSlotId.HasValue)
                        throw new InvalidOperationException("El puesto seleccionado ya no está libre. Elige otro.");
                    return null; // Parqueadero lleno
                }

                // 2. CREAR LA SESIÓN (Y ASIGNAR EL ID DEL SLOT AQUÍ)
                DateTime entryTime = DateTime.Now;
                var session = new parking_session
                {
                    plate = normalized,
                    session_code = LocalTimeSessionPrefix + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
                    qr_data = $"SESSION-{Guid.NewGuid():N}".ToUpper(),
                    entry_time = entryTime,
                    status = "active",
                    vehicle_type_id = vehicleTypeId,
                    entry_operator_id = CurrentUser.Id,
                    created_by = CurrentUser.Id,
                    created_at = entryTime,
                    is_deleted = false,

                    // --- ASIGNACIÓN CORRECTA DEL SLOT EN LA SESIÓN ---
                    parking_slot_id = availableSlot.id
                };

                if (plateImage?.Length > 0)
                {
                    photoPath = await _photoStore.SaveAsync(plateImage, session.session_code);
                    session.entry_photo_path = photoPath;
                }

                await _sessionRepo.AddAsync(session);

                // 3. ACTUALIZAR EL PUESTO (Enlazar sesión y marcar como ocupado)
                availableSlot.is_occupied = true;
                availableSlot.current_session = session;
                availableSlot.updated_at = entryTime;

                await _slotRepo.UpdateAsync(availableSlot);

                // 4. GUARDAR CAMBIOS
                bool success = await _sessionRepo.SaveChangesAsync();

                if (!success)
                    throw new InvalidOperationException("La base de datos no confirmó el registro de la entrada.");

                return availableSlot.slot_number; // Retorna el número del puesto (ej: "A1")
            }
            catch
            {
                if (photoPath != null) _photoStore.Delete(photoPath);
                throw;
            }
        }

        public async Task<bool> RegisterExitByPlateAsync(string plateNumber)
        {
            var session = await _sessionRepo.GetActiveSessionByPlateAsync(plateNumber.Trim().ToUpperInvariant());
            return await FinalizeSession(session);
        }

        public async Task<bool> RegisterExitByQrAsync(string qrCode)
        {
            var session = await _sessionRepo.GetActiveSessionByQrAsync(qrCode.Trim());
            return await FinalizeSession(session);
        }

        private async Task<bool> FinalizeSession(parking_session? session)
        {
            if (session == null) return false;

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

            session.exit_time = exitTime;
            TimeSpan duration = exitTime - entryTime;

            // 3. CÁLCULO ESTRICTO: Hora o Fracción
            decimal hoursToCharge = (decimal)Math.Ceiling(duration.TotalHours);
            if (hoursToCharge < 1) hoursToCharge = 1;

            session.duration_minutes = (int)duration.TotalMinutes;
            session.amount_due = hoursToCharge * 1.00m;

            // 4. Cerrar sesión
            session.status = "paid";
            session.updated_at = exitTime;
            session.exit_operator_id = CurrentUser.Id;

            await _sessionRepo.UpdateAsync(session);

            // 5. LIBERAR EL PUESTO DE ESTACIONAMIENTO
            // La sesión conserva su parking_slot_id aunque current_session_id no se haya sincronizado.
            // Usar ese id garantiza que una salida QR libere el puesto correcto.
            var occupiedSlot = session.parking_slot_id.HasValue
                ? await _slotRepo.GetByIdAsync(session.parking_slot_id.Value)
                : null;

            if (occupiedSlot != null)
            {
                occupiedSlot.is_occupied = false;
                occupiedSlot.current_session_id = null;
                occupiedSlot.current_session = null;
                occupiedSlot.updated_at = exitTime;
                await _slotRepo.UpdateAsync(occupiedSlot);
            }

            return await _sessionRepo.SaveChangesAsync();
        }

        public async Task<parking_session?> GetActiveSessionByQrAsync(string qrCode)
        {
            return await _sessionRepo.GetActiveSessionByQrAsync(qrCode.Trim());
        }
    }
}
