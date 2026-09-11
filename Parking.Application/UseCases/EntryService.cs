using Parking.Application.EntityService;
using Parking.Application.Services;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Parking.Application.UseCases
{
    public class EntryService : IEntryService
    {
        private readonly Iparking_sessionRepository _sessionRepo;
        private readonly IParkingSlotRepository _slotRepo;

        public EntryService(
            Iparking_sessionRepository sessionRepo,
            IParkingSlotRepository slotRepo)
        {
            _sessionRepo = sessionRepo;
            _slotRepo = slotRepo;
        }

        public async Task<parking_session?> GetActiveSessionByPlateAsync(string plateNumber)
        {
            return await _sessionRepo.GetActiveSessionByPlateAsync(plateNumber.Trim().ToUpperInvariant());
        }

        public async Task<string?> RegisterEntryAsync(string plateNumber, int vehicleTypeId)
        {
            if (string.IsNullOrWhiteSpace(plateNumber))
                return null;

            try
            {
                string normalized = plateNumber.Trim().ToUpperInvariant();

                var activeSession = await _sessionRepo.GetActiveSessionByPlateAsync(normalized);
                if (activeSession != null)
                    return "EXISTENTE";

                // 1. BUSCAR PUESTO LIBRE AUTOMÁTICAMENTE
                var slots = await _slotRepo.GetAllAsync();
                var availableSlot = slots.FirstOrDefault(s => !s.is_occupied);

                if (availableSlot == null)
                    return null; // Parqueadero lleno

                // 2. CREAR LA SESIÓN (Y ASIGNAR EL ID DEL SLOT AQUÍ)
                var session = new parking_session
                {
                    plate = normalized,
                    session_code = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    qr_data = $"SESSION-{Guid.NewGuid():N}".ToUpper(),
                    entry_time = DateTime.UtcNow,
                    status = "active",
                    vehicle_type_id = vehicleTypeId,
                    entry_operator_id = CurrentUser.Id,
                    created_by = CurrentUser.Id,
                    created_at = DateTime.UtcNow,
                    is_deleted = false,

                    // --- ASIGNACIÓN CORRECTA DEL SLOT EN LA SESIÓN ---
                    parking_slot_id = availableSlot.id
                };

                await _sessionRepo.AddAsync(session);

                // 3. ACTUALIZAR EL PUESTO (Enlazar sesión y marcar como ocupado)
                availableSlot.is_occupied = true;
                availableSlot.current_session = session;
                availableSlot.updated_at = DateTime.UtcNow;

                await _slotRepo.UpdateAsync(availableSlot);

                // 4. GUARDAR CAMBIOS
                bool success = await _sessionRepo.SaveChangesAsync();

                if (success)
                    return availableSlot.slot_number; // Retorna el número del puesto (ej: "A1")

                return null;
            }
            catch
            {
                return null;
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

            // 1. Establecer hora de salida
            session.exit_time = DateTime.UtcNow;

            // 2. Calcular duración real
            TimeSpan duration = session.exit_time.Value - session.entry_time;

            // 3. CÁLCULO ESTRICTO: Hora o Fracción
            decimal hoursToCharge = (decimal)Math.Ceiling(duration.TotalHours);
            if (hoursToCharge < 1) hoursToCharge = 1;

            session.duration_minutes = (int)duration.TotalMinutes;
            session.amount_due = hoursToCharge * 1.00m;

            // 4. Cerrar sesión
            session.status = "paid";
            session.updated_at = DateTime.UtcNow;
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
                occupiedSlot.updated_at = DateTime.UtcNow;
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
