using Microsoft.Data.SqlClient;
using Parking.Application.UseCases;
using System.Data;

namespace Parking.UI.Windows.Services;

/// <summary>Registra cada cuota mensual recibida en un asiento independiente y actualiza el contrato en la misma transacción.</summary>
public sealed class MonthlyFeeLedgerService
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private bool _schemaReady;

    /// <summary>Utiliza la misma base configurada para las operaciones del parqueadero.</summary>
    /// <param name="settings">Conexión protegida de esta instalación.</param>
    public MonthlyFeeLedgerService(ApplicationSettingsStore settings)
    {
        // La cadena se toma del perfil del equipo; nunca se incluye una dirección SQL fija.
        _connectionString = settings.Load().ConnectionString;
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("No hay conexión SQL configurada.");
    }

    /// <summary>Prepara el libro de cobros mensuales sin alterar payments ni sesiones existentes.</summary>
    /// <returns>Tarea que concluye cuando existe la tabla o comunica un error de conexión o permisos.</returns>
    public async Task EnsureSchemaAsync()
    {
        // La marca y el semáforo evitan ejecutar DDL repetido en la misma instalación abierta.
        if (_schemaReady) return;
        await _schemaGate.WaitAsync();
        try
        {
            if (_schemaReady) return;
            // La creación condicional admite instalaciones antiguas que aún no tienen esta tabla.
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand("""
                IF OBJECT_ID(N'dbo.parking_monthly_fee_receipts', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.parking_monthly_fee_receipts (
                        id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        plan_id int NOT NULL REFERENCES dbo.vehicle_monthly_plans(id),
                        plate nvarchar(20) NOT NULL,
                        amount decimal(10,2) NOT NULL,
                        payment_method nvarchar(20) NOT NULL,
                        collected_by int NOT NULL REFERENCES dbo.users(id),
                        collected_at datetime2 NOT NULL,
                        period_end_date date NOT NULL,
                        CONSTRAINT UQ_parking_monthly_fee_receipt_period UNIQUE(plan_id, period_end_date),
                        CONSTRAINT CK_parking_monthly_fee_receipt_amount CHECK(amount > 0)
                    );
                    CREATE INDEX IX_parking_monthly_fee_receipts_collected_at
                        ON dbo.parking_monthly_fee_receipts(collected_at);
                END
                """, connection);
            await command.ExecuteNonQueryAsync();
            _schemaReady = true;
        }
        finally { _schemaGate.Release(); }
    }

    /// <summary>Confirma una cuota vencida y deja el contrato con sus fechas y estado originales.</summary>
    /// <param name="planId">Contrato seleccionado.</param>
    /// <param name="operatorId">Usuario que declara haber recibido el dinero.</param>
    /// <param name="paymentMethod">Medio real elegido por el operador.</param>
    /// <returns>Asiento mensual confirmado con el importe exacto.</returns>
    public async Task<MonthlyFeeReceipt> RecordOverdueAsync(int planId, int operatorId, string paymentMethod)
    {
        // Solo se admite un contrato real y un usuario identificado para la trazabilidad.
        if (planId <= 0 || operatorId <= 0)
            throw new ArgumentException("Seleccione un contrato y un operador válidos.");
        // El valor se normaliza antes de persistirlo para que Caja agrupe el mismo medio correctamente.
        string method = paymentMethod?.Trim().ToLowerInvariant() switch
        {
            "cash" => "cash",
            "transfer" => "transfer",
            "card" => "card",
            _ => throw new ArgumentException("Seleccione efectivo, transferencia o tarjeta.")
        };
        // La tabla debe estar disponible antes de abrir la transacción del cobro.
        await EnsureSchemaAsync();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            // El bloqueo protege la misma cuota frente a dos operadores o dos clics simultáneos.
            await using var read = new SqlCommand("""
                SELECT p.monthly_fee, p.end_date, p.payment_date, v.plate, u.full_name
                FROM dbo.vehicle_monthly_plans p WITH (UPDLOCK, HOLDLOCK)
                JOIN dbo.registered_vehicles v ON v.id = p.registered_vehicle_id
                JOIN dbo.users u ON u.id = @operator
                WHERE p.id = @plan AND p.is_deleted = 0 AND v.is_deleted = 0
                """, connection, transaction);
            read.Parameters.Add("@plan", SqlDbType.Int).Value = planId;
            read.Parameters.Add("@operator", SqlDbType.Int).Value = operatorId;
            decimal amount;
            DateTime endDate;
            DateTime lastPaid;
            string plate;
            string operatorName;
            await using (var reader = await read.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("No se encontró el contrato mensual o el operador.");
                amount = reader.GetDecimal(0);
                endDate = reader.GetDateTime(1);
                lastPaid = reader.GetDateTime(2);
                plate = reader.GetString(3);
                operatorName = reader.GetString(4);
            }

            // Un contrato vigente o una cuota ya pagada no genera un segundo asiento.
            DateTime collectedAt = DateTime.Now;
            if (collectedAt.Date <= endDate.Date || lastPaid.Date > endDate.Date)
                throw new InvalidOperationException("Este contrato no tiene una cuota vencida pendiente.");
            MoneyAmount.RequireValid(amount, "La cuota mensual");
            if (amount <= 0)
                throw new InvalidOperationException("La cuota mensual debe ser mayor que cero.");

            // Un asiento por período vencido; la clave única también impide duplicados en SQL.
            await using var insert = new SqlCommand("""
                INSERT INTO dbo.parking_monthly_fee_receipts
                    (plan_id, plate, amount, payment_method, collected_by, collected_at, period_end_date)
                OUTPUT INSERTED.id
                VALUES (@plan, @plate, @amount, @method, @operator, @at, @periodEnd)
                """, connection, transaction);
            insert.Parameters.Add("@plan", SqlDbType.Int).Value = planId;
            insert.Parameters.Add("@plate", SqlDbType.NVarChar, 20).Value = plate;
            var amountParameter = insert.Parameters.Add("@amount", SqlDbType.Decimal);
            amountParameter.Precision = 10;
            amountParameter.Scale = 2;
            amountParameter.Value = amount;
            insert.Parameters.Add("@method", SqlDbType.NVarChar, 20).Value = method;
            insert.Parameters.Add("@operator", SqlDbType.Int).Value = operatorId;
            insert.Parameters.Add("@at", SqlDbType.DateTime2).Value = collectedAt;
            insert.Parameters.Add("@periodEnd", SqlDbType.Date).Value = endDate.Date;
            long receiptId = Convert.ToInt64(await insert.ExecuteScalarAsync());

            // El cambio de payment_date solo se confirma si también quedó guardado el asiento.
            await using var update = new SqlCommand("""
                UPDATE dbo.vehicle_monthly_plans
                SET payment_date = @at, collected_by = @operator,
                    updated_at = @at, updated_by = @operator
                WHERE id = @plan
                """, connection, transaction);
            update.Parameters.Add("@at", SqlDbType.DateTime2).Value = collectedAt;
            update.Parameters.Add("@operator", SqlDbType.Int).Value = operatorId;
            update.Parameters.Add("@plan", SqlDbType.Int).Value = planId;
            if (await update.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("No se pudo actualizar el contrato mensual.");

            // Solo al confirmar ambas escrituras se comunica el cobro como realizado.
            await transaction.CommitAsync();
            return new MonthlyFeeReceipt(receiptId, planId, plate, operatorName, amount, method,
                collectedAt, endDate.Date);
        }
        catch
        {
            // Cualquier fallo anterior al commit revierte tanto el asiento como la fecha de pago.
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>Consulta cuotas explícitamente cobradas en un intervalo con fin exclusivo.</summary>
    /// <param name="fromInclusive">Inicio del período.</param>
    /// <param name="toExclusive">Primer instante fuera del período.</param>
    /// <returns>Asientos mensuales ordenados del más reciente al más antiguo.</returns>
    public async Task<IReadOnlyList<MonthlyFeeReceipt>> GetReceiptsAsync(DateTime fromInclusive, DateTime toExclusive)
    {
        // Se mantiene el límite superior exclusivo para no repetir cobros entre días contiguos.
        await EnsureSchemaAsync();
        var rows = new List<MonthlyFeeReceipt>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT r.id, r.plan_id, r.plate, u.full_name, r.amount,
                r.payment_method, r.collected_at, r.period_end_date
            FROM dbo.parking_monthly_fee_receipts r
            JOIN dbo.users u ON u.id = r.collected_by
            WHERE r.collected_at >= @from AND r.collected_at < @to
            ORDER BY r.collected_at DESC, r.id DESC
            """, connection);
        command.Parameters.Add("@from", SqlDbType.DateTime2).Value = fromInclusive;
        command.Parameters.Add("@to", SqlDbType.DateTime2).Value = toExclusive;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(new MonthlyFeeReceipt(reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2),
                reader.GetString(3), reader.GetDecimal(4), reader.GetString(5), reader.GetDateTime(6),
                reader.GetDateTime(7)));
        return rows;
    }
}
