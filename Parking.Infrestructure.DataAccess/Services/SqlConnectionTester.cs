using Microsoft.Data.SqlClient;
using Parking.Application.Interfaces;

namespace Parking.Infrastructure.DataAccess.Services;

/// <summary>Prueba la conexión SQL configurada sin escribir datos.</summary>
public sealed class SqlConnectionTester : ISqlConnectionTester
{
    /// <summary>Comprueba servidor y base con el analizador oficial de SqlClient.</summary>
    /// <param name="candidate">Cadena propuesta.</param>
    /// <param name="normalized">Cadena resultante o texto vacío.</param>
    /// <returns>Verdadero cuando hay datos mínimos de conexión.</returns>
    public bool TryNormalize(string candidate, out string normalized)
    {
        normalized = string.Empty;
        try
        {
            var builder = new SqlConnectionStringBuilder(candidate);
            if (string.IsNullOrWhiteSpace(builder.DataSource)
                || string.IsNullOrWhiteSpace(builder.InitialCatalog)) return false;
            normalized = builder.ConnectionString;
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    /// <summary>Abre y cierra la base solicitada con el mismo límite anterior de cinco segundos.</summary>
    /// <param name="connectionString">Cadena ya validada.</param>
    /// <returns>Base confirmada por el servidor.</returns>
    public async Task<string> TestAsync(string connectionString)
    {
        // La cadena de prueba nunca se persiste desde este servicio.
        var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        return connection.Database;
    }
}
