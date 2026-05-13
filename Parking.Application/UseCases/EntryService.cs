using Parking.Application.EntityService;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using System;
using System.Threading.Tasks;

namespace Parking.Application.UseCases
{
    public class EntryService : IEntryService
    {
        private readonly IParkingSessionRepository _sessionRepo;

        public EntryService(IParkingSessionRepository sessionRepo)
        {
            _sessionRepo = sessionRepo;
        }

        public async Task<ParkingSession?> GetActiveSessionByPlateAsync(string plateNumber)
        {
            return await _sessionRepo.GetActiveSessionByPlateAsync(plateNumber.Trim().ToUpperInvariant());
        }

        public async Task<bool> RegisterEntryAsync(string plateNumber)
        {
            if (string.IsNullOrWhiteSpace(plateNumber)) return false;
            try
            {
                string normalized = plateNumber.Trim().ToUpperInvariant();
                var activeSession = await _sessionRepo.GetActiveSessionByPlateAsync(normalized);
                if (activeSession != null) return true;

                var session = new ParkingSession
                {
                    plate = normalized,
                    session_code = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    qr_data = $"SESSION-{Guid.NewGuid():N}".ToUpper(),
                    entry_time = DateTime.UtcNow,
                    status = "active",
                    vehicle_type_id = 1,
                    entry_operator_id = 1,
                    created_at = DateTime.UtcNow,
                    is_deleted = false
                };

                await _sessionRepo.AddAsync(session);
                return await _sessionRepo.SaveChangesAsync();
            }
            catch { return false; }
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

        private async Task<bool> FinalizeSession(ParkingSession? session)
        {
            if (session == null) return false;

            // 1. Establecer hora de salida
            session.exit_time = DateTime.UtcNow;

            // 2. Calcular duración real
            TimeSpan duration = session.exit_time.Value - session.entry_time;

            // 3. CÁLCULO ESTRICTO: Hora o Fracción
            // Si duration.TotalHours es 5.01, Math.Ceiling devuelve 6.
            decimal hoursToCharge = (decimal)Math.Ceiling(duration.TotalHours);

            // Garantizar cobro mínimo de 1 hora si el tiempo es muy corto
            if (hoursToCharge < 1) hoursToCharge = 1;

            session.duration_minutes = (int)duration.TotalMinutes;
            session.amount_due = hoursToCharge * 1.00m; // Tarifa de $1 por cada hora/fracción

            // 4. Cerrar sesión
            session.status = "paid";
            session.updated_at = DateTime.UtcNow;

            await _sessionRepo.UpdateAsync(session);
            return await _sessionRepo.SaveChangesAsync();
        }

        public async Task<ParkingSession?> GetActiveSessionByQrAsync(string qrCode)
        {
            return await _sessionRepo.GetActiveSessionByQrAsync(qrCode.Trim());
        }
    }
}