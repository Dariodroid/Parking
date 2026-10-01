using Parking.Domain.Model.Models;

namespace Parking.Application.Interfaces;

/// <summary>Casos de uso para administrar las cuentas de operadores.</summary>
public interface IUserManagementService
{
    Task<IEnumerable<user>> GetAllAsync();
    Task<user?> GetByIdAsync(long id);
    /// <summary>Devuelve falso cuando el nombre ya pertenece a otra cuenta.</summary>
    Task<bool> CreateAsync(user account, string password);
    Task UpdateAsync(user account, string? newPassword);
    Task DeleteAsync(user account, int operatorId);
}
