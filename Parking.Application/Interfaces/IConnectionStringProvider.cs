namespace Parking.Application.Interfaces;

/// <summary>Entrega la conexión configurada en esta instalación sin exponer cómo se guarda.</summary>
public interface IConnectionStringProvider
{
    /// <summary>Obtiene la cadena de conexión de la instalación actual.</summary>
    /// <returns>Cadena de conexión o texto vacío si falta configuración.</returns>
    string GetConnectionString();
}
