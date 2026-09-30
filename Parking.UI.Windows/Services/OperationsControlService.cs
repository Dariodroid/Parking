using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parking.Infrastructure.DataAccess;
using System.Data;

namespace Parking.UI.Windows.Services;

/// <summary>Detecta excepciones operativas y conserva revisiones y cierres sin alterar los cobros originales.</summary>
public sealed class OperationsControlService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _connectionString;
    private readonly MonthlyFeeLedgerService _monthlyLedger;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private bool _schemaReady;

    /// <summary>Usa la misma base SQL configurada para las operaciones del parqueadero.</summary>
    /// <param name="scopeFactory">Crea un contexto independiente para cada consulta EF.</param>
    /// <param name="settings">Conexión protegida de la instalación actual.</param>
    /// <param name="monthlyLedger">Libro de cuotas que participa en los cierres de caja.</param>
    public OperationsControlService(IServiceScopeFactory scopeFactory, ApplicationSettingsStore settings,
        MonthlyFeeLedgerService monthlyLedger)
    {
        _scopeFactory = scopeFactory;
        _monthlyLedger = monthlyLedger;
        _connectionString = settings.Load().ConnectionString;
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("No hay conexión SQL configurada.");
    }

    /// <summary>Crea las dos tablas auxiliares una sola vez, sin tocar sesiones, usuarios ni pagos.</summary>
    public async Task EnsureSchemaAsync()
    {
        if (_schemaReady)
        {
            await _monthlyLedger.EnsureSchemaAsync();
            return;
        }
        await _schemaGate.WaitAsync();
        try
        {
            if (!_schemaReady)
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new SqlCommand(SchemaSql, connection);
                await command.ExecuteNonQueryAsync();
                _schemaReady = true;
            }
        }
        finally { _schemaGate.Release(); }
        await _monthlyLedger.EnsureSchemaAsync();
    }

    /// <summary>Busca cobros ausentes o distintos y sesiones ocasionales abiertas más de 48 horas.</summary>
    /// <returns>Incidencias no atendidas de los últimos 90 días y sesiones todavía abiertas.</returns>
    public async Task<IReadOnlyList<ControlIncident>> GetOpenIncidentsAsync()
    {
        await EnsureSchemaAsync();
        // Esta consulta usa un DbContext propio, separado del registro de entradas y salidas en directo.
        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<parking_dbContext>();
        DateTime since = DateTime.Now.AddDays(-90);
        DateTime stale = DateTime.Now.AddHours(-48);

        // Solo los cierres de pago reciente entran en la conciliación; las estancias abiertas se revisan aparte.
        var sessions = await database.parking_sessions.AsNoTracking()
            .Where(s => !s.is_deleted &&
                ((s.status == "paid" && s.exit_time >= since && s.amount_due > 0) ||
                 (s.status == "active" && s.entry_time < stale && s.registered_vehicle_id == null)))
            .Select(s => new { s.id, s.plate, s.status, s.entry_time, s.exit_time, s.amount_due,
                s.entry_photo_path,
                EntryOperator = s.entry_operator.full_name,
                ExitOperator = s.exit_operator != null ? s.exit_operator.full_name : string.Empty })
            .ToListAsync();

        // Se agrupan pagos por sesión, excluyendo asientos eliminados de los importes vigentes.
        var paidIds = sessions.Where(s => s.status == "paid").Select(s => s.id).ToArray();
        var paidTotals = await database.payments.AsNoTracking()
            .Where(p => !p.is_deleted && paidIds.Contains(p.session_id))
            .GroupBy(p => p.session_id)
            .Select(g => new { SessionId = g.Key, Amount = g.Sum(p => p.amount_paid) })
            .ToDictionaryAsync(x => x.SessionId, x => x.Amount);

        var incidents = new List<ControlIncident>();
        foreach (var session in sessions)
        {
            if (session.status == "active")
            {
                incidents.Add(new ControlIncident($"stale:{session.id}", "Sesión prolongada", session.plate,
                    session.entry_time, "Entrada sin salida registrada durante más de 48 horas.", 0m,
                    session.EntryOperator, session.entry_photo_path));
                continue;
            }

            decimal paid = paidTotals.GetValueOrDefault(session.id);
            decimal difference = (session.amount_due ?? 0m) - paid;
            if (Math.Abs(difference) >= 0.01m)
                incidents.Add(new ControlIncident($"payment:{session.id}", "Diferencia de cobro", session.plate,
                    session.exit_time ?? session.entry_time,
                    $"Debido: {CurrencyDisplay.Format(session.amount_due ?? 0m)} · registrado en payments: {CurrencyDisplay.Format(paid)}.",
                    difference, session.ExitOperator, session.entry_photo_path));
        }

        // La misma regla de la ficha del cliente: cuota pendiente si venció y no hay pago posterior al fin.
        var overduePlans = await database.vehicle_monthly_plans.AsNoTracking()
            .Where(p => !p.is_deleted && p.end_date.Date < DateTime.Today
                && p.payment_date.Date <= p.end_date.Date && p.monthly_fee > 0
                && !p.registered_vehicle.is_deleted)
            .Select(p => new { p.id, p.end_date, p.monthly_fee,
                p.registered_vehicle.plate, p.registered_vehicle.owner_name })
            .ToListAsync();
        foreach (var plan in overduePlans)
            incidents.Add(new ControlIncident($"debt:{plan.id}", "Cuota vencida", plan.plate,
                plan.end_date, $"{plan.owner_name}: cuota pendiente del contrato vencido el {plan.end_date:dd/MM/yyyy}.",
                plan.monthly_fee, string.Empty));

        // Una revisión oculta esa incidencia de la bandeja, pero preserva el asiento y su explicación.
        var reviewed = await GetReviewedKeysAsync();
        return incidents.Where(i => !reviewed.Contains(i.Key))
            .OrderByDescending(i => Math.Abs(i.Difference)).ThenBy(i => i.OccurredAt).ToList();
    }

    /// <summary>Firma la atención de una incidencia con operador, fecha y motivo obligatorio.</summary>
    /// <param name="key">Identificador estable de la incidencia detectada.</param>
    /// <param name="reason">Explicación de la revisión, de 8 a 500 caracteres.</param>
    /// <param name="operatorId">Usuario autenticado que la atendió.</param>
    public async Task ReviewAsync(string key, string reason, int operatorId)
    {
        reason = reason.Trim();
        if (operatorId <= 0 || key.Length > 100 || reason.Length is < 8 or > 500)
            throw new ArgumentException("Indique un motivo de 8 a 500 caracteres.");
        await EnsureSchemaAsync();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO dbo.parking_incident_reviews (incident_key, reviewed_by, reason) VALUES (@key, @user, @reason)", connection);
        command.Parameters.Add("@key", SqlDbType.NVarChar, 100).Value = key;
        command.Parameters.Add("@user", SqlDbType.Int).Value = operatorId;
        command.Parameters.Add("@reason", SqlDbType.NVarChar, 500).Value = reason;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Consulta las últimas decisiones sin eliminar datos operativos.</summary>
    public async Task<IReadOnlyList<IncidentReview>> GetReviewsAsync()
    {
        await EnsureSchemaAsync();
        var reviews = new List<IncidentReview>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT TOP (100) r.incident_key, r.reviewed_at, u.full_name, r.reason
            FROM dbo.parking_incident_reviews r JOIN dbo.users u ON u.id = r.reviewed_by
            ORDER BY r.reviewed_at DESC
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            reviews.Add(new IncidentReview(reader.GetString(0), reader.GetDateTime(1),
                reader.GetString(2), reader.GetString(3)));
        return reviews;
    }

    /// <summary>Calcula el turno abierto del usuario desde su último cierre o la medianoche actual.</summary>
    /// <param name="operatorId">Usuario que recibió los pagos.</param>
    public async Task<ShiftSummary> GetCurrentShiftAsync(int operatorId)
    {
        await EnsureSchemaAsync();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        DateTime end = DateTime.Now;
        DateTime start = await GetShiftStartAsync(connection, null, operatorId, end);
        return await CalculateShiftAsync(connection, null, operatorId, start, end);
    }

    /// <summary>Guarda un cierre de turno con el efectivo realmente contado y su diferencia.</summary>
    /// <param name="operatorId">Usuario autenticado que firma su propio turno.</param>
    /// <param name="countedCash">Cantidad física contada en caja.</param>
    /// <param name="note">Explicación obligatoria cuando hay diferencia.</param>
    /// <returns>Instantánea inmutable del cierre guardado.</returns>
    public async Task<ShiftClosure> CloseShiftAsync(int operatorId, decimal countedCash, string note)
    {
        if (operatorId <= 0 || countedCash < 0 || countedCash > 999999999m
            || decimal.Truncate(countedCash * 100m) != countedCash * 100m)
            throw new ArgumentException("El efectivo contado debe ser positivo y tener como máximo dos decimales.");
        note = note.Trim();
        if (note.Length > 500) throw new ArgumentException("La observación excede 500 caracteres.");
        await EnsureSchemaAsync();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        DateTime end = DateTime.Now;
        DateTime start = await GetShiftStartAsync(connection, transaction, operatorId, end);
        ShiftSummary totals = await CalculateShiftAsync(connection, transaction, operatorId, start, end);
        decimal difference = countedCash - totals.Cash;
        if (difference != 0m && note.Length < 8)
            throw new ArgumentException("Explique la diferencia con al menos 8 caracteres.");

        // Los importes quedan congelados al cerrar; consultas futuras no reescriben este asiento.
        await using var insert = new SqlCommand("""
            INSERT INTO dbo.parking_shift_closures
                (operator_id, started_at, closed_at, expected_cash, counted_cash, difference,
                 transfer_total, card_total, other_total, payment_count, note)
            OUTPUT INSERTED.id
            VALUES (@user, @start, @end, @cash, @counted, @difference,
                    @transfer, @card, @other, @count, @note)
            """, connection, transaction);
        insert.Parameters.Add("@user", SqlDbType.Int).Value = operatorId;
        insert.Parameters.Add("@start", SqlDbType.DateTime2).Value = start;
        insert.Parameters.Add("@end", SqlDbType.DateTime2).Value = end;
        AddMoney(insert, "@cash", totals.Cash);
        AddMoney(insert, "@counted", countedCash);
        AddMoney(insert, "@difference", difference);
        AddMoney(insert, "@transfer", totals.Transfer);
        AddMoney(insert, "@card", totals.Card);
        AddMoney(insert, "@other", totals.Other);
        insert.Parameters.Add("@count", SqlDbType.Int).Value = totals.PaymentCount;
        insert.Parameters.Add("@note", SqlDbType.NVarChar, 500).Value = note;
        long id = Convert.ToInt64(await insert.ExecuteScalarAsync());
        await transaction.CommitAsync();
        return new ShiftClosure(id, string.Empty, start, end, totals.Cash, countedCash, difference,
            totals.Transfer, totals.Card, totals.Other, note);
    }

    /// <summary>Recupera los cierres recientes para supervisión y trazabilidad.</summary>
    /// <param name="operatorId">Si se indica, solo muestra los cierres de ese operador.</param>
    public async Task<IReadOnlyList<ShiftClosure>> GetClosuresAsync(int? operatorId = null)
    {
        await EnsureSchemaAsync();
        var closures = new List<ShiftClosure>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT TOP (100) c.id, u.full_name, c.started_at, c.closed_at,
                c.expected_cash, c.counted_cash, c.difference, c.transfer_total,
                c.card_total, c.other_total, c.note
            FROM dbo.parking_shift_closures c JOIN dbo.users u ON u.id = c.operator_id
            WHERE (@user IS NULL OR c.operator_id = @user)
            ORDER BY c.closed_at DESC, c.id DESC
            """, connection);
        command.Parameters.Add("@user", SqlDbType.Int).Value = operatorId.HasValue ? operatorId.Value : DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            closures.Add(new ShiftClosure(reader.GetInt64(0), reader.GetString(1), reader.GetDateTime(2),
                reader.GetDateTime(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6),
                reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetString(10)));
        return closures;
    }

    /// <summary>Recupera las claves de incidencias ya atendidas para ocultarlas de la bandeja abierta.</summary>
    /// <returns>Conjunto de claves firmadas, sin alterar los registros de origen.</returns>
    private async Task<HashSet<string>> GetReviewedKeysAsync()
    {
        var keys = new HashSet<string>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT incident_key FROM dbo.parking_incident_reviews", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) keys.Add(reader.GetString(0));
        return keys;
    }

    /// <summary>Obtiene el comienzo del turno desde el último cierre o la medianoche actual.</summary>
    /// <param name="connection">Conexión SQL ya abierta.</param>
    /// <param name="transaction">Transacción del cierre, si se está confirmando uno.</param>
    /// <param name="operatorId">Usuario cuyo turno se calcula.</param>
    /// <param name="end">Momento final de la consulta.</param>
    /// <returns>Inicio efectivo del intervalo que todavía no ha sido cerrado.</returns>
    private static async Task<DateTime> GetShiftStartAsync(SqlConnection connection, SqlTransaction? transaction,
        int operatorId, DateTime end)
    {
        await using var command = new SqlCommand("""
            SELECT TOP (1) closed_at FROM dbo.parking_shift_closures WITH (UPDLOCK, HOLDLOCK)
            WHERE operator_id = @user AND closed_at <= @end ORDER BY closed_at DESC, id DESC
            """, connection, transaction);
        command.Parameters.Add("@user", SqlDbType.Int).Value = operatorId;
        command.Parameters.Add("@end", SqlDbType.DateTime2).Value = end;
        object? last = await command.ExecuteScalarAsync();
        DateTime midnight = end.Date;
        return last is DateTime at && at > midnight ? at : midnight;
    }

    /// <summary>Suma por medio de pago las salidas y las cuotas mensuales cobradas en el turno.</summary>
    /// <param name="connection">Conexión SQL ya abierta.</param>
    /// <param name="transaction">Transacción del cierre, si existe.</param>
    /// <param name="operatorId">Operador que recibió los pagos.</param>
    /// <param name="start">Inicio incluido del turno.</param>
    /// <param name="end">Fin excluido del turno.</param>
    /// <returns>Importes exactos y número de cobros por medio de pago.</returns>
    private static async Task<ShiftSummary> CalculateShiftAsync(SqlConnection connection, SqlTransaction? transaction,
        int operatorId, DateTime start, DateTime end)
    {
        decimal cash = 0, transfer = 0, card = 0, other = 0;
        int count = 0;
        await using var command = new SqlCommand("""
            SELECT payment_method, SUM(amount), COUNT(*)
            FROM (
                SELECT payment_method, amount_paid AS amount FROM dbo.payments
                WHERE is_deleted = 0 AND collected_by = @user
                    AND collected_at >= @start AND collected_at < @end
                UNION ALL
                SELECT payment_method, amount FROM dbo.parking_monthly_fee_receipts
                WHERE collected_by = @user
                    AND collected_at >= @start AND collected_at < @end
            ) AS receipts
            GROUP BY payment_method
            """, connection, transaction);
        command.Parameters.Add("@user", SqlDbType.Int).Value = operatorId;
        command.Parameters.Add("@start", SqlDbType.DateTime2).Value = start;
        command.Parameters.Add("@end", SqlDbType.DateTime2).Value = end;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string method = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            decimal amount = reader.GetDecimal(1);
            count += reader.GetInt32(2);
            switch (method)
            {
                case "cash": cash += amount; break;
                case "transfer": transfer += amount; break;
                case "card": card += amount; break;
                default: other += amount; break;
            }
        }
        return new ShiftSummary(start, end, cash, transfer, card, other, count);
    }

    /// <summary>Agrega un parámetro decimal sin permitir que SQL redondee fracciones de centavo.</summary>
    /// <param name="command">Comando que guarda el cierre.</param>
    /// <param name="name">Nombre SQL del parámetro monetario.</param>
    /// <param name="value">Importe exacto que se debe persistir.</param>
    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        // La escala SQL no debe redondear silenciosamente ningún importe del cierre.
        if (decimal.Round(value, 2) != value)
            throw new ArgumentOutOfRangeException(nameof(value), $"El importe {name} contiene fracciones de centavo.");
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value;
    }

    private const string SchemaSql = """
        IF OBJECT_ID(N'dbo.parking_incident_reviews', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.parking_incident_reviews (
                incident_key nvarchar(100) NOT NULL PRIMARY KEY,
                reviewed_at datetime2 NOT NULL CONSTRAINT DF_parking_incident_reviews_at DEFAULT (SYSDATETIME()),
                reviewed_by int NOT NULL REFERENCES dbo.users(id),
                reason nvarchar(500) NOT NULL
            );
        END;
        IF OBJECT_ID(N'dbo.parking_shift_closures', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.parking_shift_closures (
                id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                operator_id int NOT NULL REFERENCES dbo.users(id),
                started_at datetime2 NOT NULL,
                closed_at datetime2 NOT NULL,
                expected_cash decimal(18,2) NOT NULL,
                counted_cash decimal(18,2) NOT NULL,
                difference decimal(18,2) NOT NULL,
                transfer_total decimal(18,2) NOT NULL,
                card_total decimal(18,2) NOT NULL,
                other_total decimal(18,2) NOT NULL,
                payment_count int NOT NULL,
                note nvarchar(500) NOT NULL
            );
            CREATE INDEX IX_parking_shift_closures_operator_time
                ON dbo.parking_shift_closures(operator_id, closed_at DESC);
        END;
        """;
}
