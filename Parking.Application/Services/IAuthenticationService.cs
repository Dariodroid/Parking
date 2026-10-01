using Parking.Application.Dto;

namespace Parking.Application.Services;

/// <summary>Expone el caso de uso de inicio de sesión a la interfaz de usuario.</summary>
public interface IAuthenticationService
{
    /// <summary>Comprueba credenciales y comunica el resultado sin revelar usuarios inexistentes.</summary>
    /// <param name="request">Credenciales introducidas por el operador.</param>
    /// <returns>Resultado de autenticación y usuario cuando el acceso es válido.</returns>
    Task<LoginResult> LoginAsync(LoginRequest request);
}
