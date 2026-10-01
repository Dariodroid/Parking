namespace Parking.Application.Interfaces;

/// <summary>Valida y prueba una conexión SQL sin exponer el proveedor a la interfaz.</summary>
public interface ISqlConnectionTester
{
    /// <summary>Comprueba los campos requeridos y normaliza la conexión.</summary>
    /// <param name="candidate">Cadena introducida por el administrador.</param>
    /// <param name="normalized">Cadena normalizada si es válida.</param>
    /// <returns>Verdadero cuando incluye servidor y base.</returns>
    bool TryNormalize(string candidate, out string normalized);

    /// <summary>Abre y cierra la conexión propuesta con un tiempo de espera corto.</summary>
    /// <param name="connectionString">Cadena válida que se va a probar.</param>
    /// <returns>Nombre de la base a la que se conectó.</returns>
    Task<string> TestAsync(string connectionString);
}
