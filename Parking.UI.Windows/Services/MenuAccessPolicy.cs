namespace Parking.UI.Windows.Services;

/// <summary>Define las secciones visibles y navegables para cada rol de la sesión.</summary>
public static class MenuAccessPolicy
{
    /// <summary>Indica si el rol puede administrar usuarios, configuración e informes.</summary>
    /// <param name="role">Rol guardado para el usuario autenticado.</param>
    /// <returns>Verdadero únicamente para Administración.</returns>
    public static bool IsAdministrator(string role) =>
        string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase);

    /// <summary>Autoriza una sección según el rol actual; los destinos desconocidos se rechazan.</summary>
    /// <param name="role">Rol guardado para el usuario autenticado.</param>
    /// <param name="destination">Clave del botón de navegación.</param>
    /// <returns>Verdadero si la sección está permitida para ese rol.</returns>
    public static bool CanNavigate(string role, string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination)) return false;
        bool administrator = IsAdministrator(role);
        bool operatorRole = string.Equals(role, "Operador", StringComparison.OrdinalIgnoreCase);
        if (!administrator && !operatorRole) return false;

        return destination switch
        {
            "Dashboard" or "Operaciones" or "Caja" or "Clientes" or "Apariencia y conexión" or "Centro de control" => true,
            "Usuarios" or "Config" or "Tipos Vehículo" or "Reporte Operadores"
                or "Reporte Vehiculos" or "Reporte Rendimiento" => administrator,
            _ => false
        };
    }
}
