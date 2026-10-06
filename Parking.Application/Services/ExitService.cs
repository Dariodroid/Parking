using Parking.Application.Interfaces;
using Parking.Application.UseCases;
using Parking.Domain.Model.Interfaces;
using Parking.Domain.Model.Models;
using Parking.Domain.Model.Policies;

namespace Parking.Application.Services;

/// <summary>Cierra estancias por placa o QR y confirma sesión, pago y puesto en la misma unidad de trabajo.</summary>
public sealed class ExitService : IExitService
{
    private readonly IParkingSessionRepository _sessionRepo;
    private readonly IParkingSlotRepository _slotRepo;
    private readonly IVehicleTypeRepository _vehicleTypes;
    private readonly IBaseRepository<payment> _paymentRepo;

    /// <summary>Recibe repositorios que comparten el mismo contexto EF para el cierre.</summary>
    public ExitService(IParkingSessionRepository sessionRepo, IParkingSlotRepository slotRepo,
        IVehicleTypeRepository vehicleTypes, IBaseRepository<payment> paymentRepo)
    {
        _sessionRepo = sessionRepo;
        _slotRepo = slotRepo;
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
        DateTime entryTime = ParkingSessionTime.NormalizeLegacyEntryTime(session);

        // Se conserva la hora real de salida y la duración total de estancia.
        session.exit_time = exitTime;
        TimeSpan duration = exitTime - entryTime;

        await CalculateExitChargeAsync(session, duration);

        // El cierre pagado y el asiento contable se confirman juntos.
        session.status = "paid";
        // La fecha y el operador identifican quién efectuó el cierre.
        session.updated_at = exitTime;
        session.exit_operator_id = CurrentUser.Id;

        // EF registra los cambios en la sesión antes de guardar el contexto.
        await _sessionRepo.UpdateAsync(session);

        await RecordPaymentAsync(session, paymentMethod, exitTime);

        // 5. LIBERAR EL PUESTO DE ESTACIONAMIENTO
        // La sesión conserva su parking_slot_id aunque current_session_id no se haya sincronizado.
        // Usar ese id garantiza que una salida QR libere el puesto correcto.
        await ReleaseSlotAsync(session, exitTime);

        // Un único SaveChanges persiste sesión, pago y puesto en la misma transacción de EF.
        return await _sessionRepo.SaveChangesAsync();
    }

    /// <summary>Conserva la modalidad de entrada y cobra por hora iniciada solo al ocasional.</summary>
    private async Task CalculateExitChargeAsync(parking_session session, TimeSpan duration)
    {
        bool monthly = session.notes == MonthlyAccessPolicy.MonthlySessionNote;
        decimal rate = 0m;
        if (!monthly)
        {
            var vehicleType = await _vehicleTypes.GetByIdAsync(session.vehicle_type_id);
            if (vehicleType == null || vehicleType.is_deleted)
                throw new InvalidOperationException("No se encontró la tarifa del tipo de vehículo.");
            rate = MoneyAmount.RequireValid(vehicleType.hourly_rate, "La tarifa por hora");
        }

        ParkingCharge charge = ParkingChargePolicy.Calculate(duration, monthly, rate);
        session.duration_minutes = charge.DurationMinutes;
        session.chargeable_minutes = charge.ChargeableMinutes;
        session.amount_due = monthly ? 0m : MoneyAmount.RequireValid(charge.AmountDue, "El cobro de salida");
    }

    /// <summary>Agrega el asiento del cobro al mismo contexto de la sesión.</summary>
    private async Task RecordPaymentAsync(parking_session session, string paymentMethod, DateTime exitTime)
    {
        if (session.amount_due is not > 0) return;
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

    /// <summary>Libera el puesto y mantiene su id histórico en la sesión cerrada.</summary>
    private async Task ReleaseSlotAsync(parking_session session, DateTime exitTime)
    {
        var slot = session.parking_slot_id.HasValue
            ? await _slotRepo.GetByIdAsync(session.parking_slot_id.Value) : null;
        if (slot == null) return;
        slot.is_occupied = false;
        slot.current_session_id = null;
        slot.current_session = null;
        slot.updated_at = exitTime;
        await _slotRepo.UpdateAsync(slot);
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
