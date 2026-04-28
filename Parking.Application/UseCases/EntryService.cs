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
                    Plate = normalized,
                    SessionCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    QrData = $"SESSION-{Guid.NewGuid():N}".ToUpper(),
                    EntryTime = DateTime.UtcNow,
                    Status = "active",
                    VehicleTypeId = 1,
                    EntryOperatorId = 1,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
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
            session.ExitTime = DateTime.UtcNow;

            // 2. Calcular duración real
            TimeSpan duration = session.ExitTime.Value - session.EntryTime;

            // 3. CÁLCULO ESTRICTO: Hora o Fracción
            // Si duration.TotalHours es 5.01, Math.Ceiling devuelve 6.
            decimal hoursToCharge = (decimal)Math.Ceiling(duration.TotalHours);

            // Garantizar cobro mínimo de 1 hora si el tiempo es muy corto
            if (hoursToCharge < 1) hoursToCharge = 1;

            session.DurationMinutes = (int)duration.TotalMinutes;
            session.AmountDue = hoursToCharge * 1.00m; // Tarifa de $1 por cada hora/fracción

            // 4. Cerrar sesión
            session.Status = "paid";
            session.UpdatedAt = DateTime.UtcNow;

            await _sessionRepo.UpdateAsync(session);
            return await _sessionRepo.SaveChangesAsync();
        }

        public async Task<ParkingSession?> GetActiveSessionByQrAsync(string qrCode)
        {
            return await _sessionRepo.GetActiveSessionByQrAsync(qrCode.Trim());
        }
    }
}