using Parking.Application.Interfaces;
using Parking.Domain.Model.Interfaces;
using Parking.Domain.Model.Models;

namespace Parking.Application.Services;

/// <summary>Coordina la administración de usuarios y conserva el hash fuera de la UI.</summary>
public sealed class UserManagementService : IUserManagementService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public UserManagementService(IUserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public Task<IEnumerable<user>> GetAllAsync() => _users.GetAllAsync();
    public Task<user?> GetByIdAsync(long id) => _users.GetByIdAsync(id);

    public async Task<bool> CreateAsync(user account, string password)
    {
        RequirePassword(password);
        if (await _users.ExistsByUsernameAsync(account.username))
            return false;
        account.password_hash = _hasher.HashPassword(password);
        await _users.AddAsync(account);
        await _users.SaveChangesAsync();
        return true;
    }

    public async Task UpdateAsync(user account, string? newPassword)
    {
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            RequirePassword(newPassword);
            account.password_hash = _hasher.HashPassword(newPassword);
            account.login_attempts = 0;
            account.locked_until = null;
        }
        await _users.UpdateAsync(account);
        await _users.SaveChangesAsync();
    }

    public async Task DeleteAsync(user account, int operatorId)
    {
        account.is_deleted = true;
        account.deleted_at = DateTime.Now;
        account.deleted_by = operatorId;
        await _users.UpdateAsync(account);
        await _users.SaveChangesAsync();
    }

    private static void RequirePassword(string password)
    {
        if (password.Length < 8)
            throw new ArgumentException("La contraseña debe tener al menos 8 caracteres.", nameof(password));
    }
}
