using Parking.Domain.Model.Models;

namespace Parking.Application.Services;

/// <summary>Identifica sesiones con hora local y adapta las fechas de sesiones antiguas.</summary>
internal static class ParkingSessionTime
{
    // Las sesiones anteriores a EC- guardaban fechas UTC sin zona.
    internal const string LocalSessionPrefix = "EC-";

    /// <summary>Convierte al horario local las sesiones anteriores al formato EC-.</summary>
    internal static DateTime NormalizeLegacyEntryTime(parking_session session)
    {
        if (session.session_code.StartsWith(LocalSessionPrefix, StringComparison.Ordinal))
            return session.entry_time;
        DateTime localEntry = DateTime.SpecifyKind(session.entry_time, DateTimeKind.Utc).ToLocalTime();
        session.entry_time = localEntry;
        session.created_at = DateTime.SpecifyKind(session.created_at, DateTimeKind.Utc).ToLocalTime();
        return localEntry;
    }
}
