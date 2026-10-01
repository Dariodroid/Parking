namespace Parking.Application.Interfaces;

/// <summary>Prepara el primer acceso únicamente en una base sin usuarios.</summary>
public interface IInitialAdministratorService
{
    /// <summary>Comprueba si todavía debe crearse la primera cuenta.</summary>
    /// <returns>Verdadero cuando la tabla de usuarios está vacía.</returns>
    Task<bool> IsRequiredAsync();

    /// <summary>Registra el primer administrador con una contraseña protegida.</summary>
    /// <param name="username">Identificador de acceso.</param>
    /// <param name="fullName">Nombre visible del administrador.</param>
    /// <param name="password">Contraseña elegida durante la instalación.</param>
    /// <returns>Verdadero si la cuenta se creó.</returns>
    Task<bool> CreateAsync(string username, string fullName, string password);
}
