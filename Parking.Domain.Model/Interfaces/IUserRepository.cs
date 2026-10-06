using Parking.Domain.Model.Models;

namespace Parking.Domain.Model.Interfaces;

/// <summary>Consulta usuarios y registra intentos de acceso sin exponer detalles de SQL.</summary>
public interface IUserRepository : IBaseRepository<user>
{
    /// <summary>Busca una cuenta activa o inactiva por su nombre de usuario.</summary>
    Task<user?> GetByUsernameAsync(string username);

    /// <summary>Indica si el nombre de usuario ya pertenece a una cuenta.</summary>
    Task<bool> ExistsByUsernameAsync(string username);

    /// <summary>Registra un acceso correcto si la cuenta no está bloqueada.</summary>
    Task<bool> UpdateLastLoginAsync(int userId, DateTime now);

    /// <summary>Cuenta un fallo y bloquea temporalmente al alcanzar el límite.</summary>
    Task RecordFailedLoginAsync(int userId, DateTime now, int maximumAttempts, TimeSpan lockDuration);
}
