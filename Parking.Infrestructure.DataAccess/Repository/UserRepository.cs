using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess.Repository;

public class userRepository : IBaseRepository<user>, IuserRepository
{
    private readonly parking_dbContext _context;

    public userRepository(parking_dbContext context)
    {
        _context = context;
    }

    public async Task<user> AddAsync(user entity)
    {
        await _context.users.AddAsync(entity);
        return entity;
    }

    public async Task<IEnumerable<user>> GetAllAsync()
    {
        return await _context.users
            .AsNoTracking()
            .Where(x => !x.is_deleted)
            .OrderBy(x => x.full_name)
            .Select(x => new user
            {
                id = x.id,
                username = x.username,
                full_name = x.full_name,
                role = x.role,
                is_active = x.is_active,
                last_login = x.last_login,
                login_attempts = x.login_attempts,
                created_at = x.created_at
            })
            .ToListAsync();
    }

    public async Task<user?> GetByIdAsync(long id)
    {
        return await _context.users
            .FirstOrDefaultAsync(x =>
                x.id == id &&
                !x.is_deleted);
    }

    public async Task<user?> GetByEmailAsync(string email)
    {
        return await _context.users
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.username == email &&
                !x.is_deleted);
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        return await _context.users
            .AnyAsync(x =>
                x.username == email &&
                !x.is_deleted);
    }

    public async Task<IEnumerable<user>> GetusersByRoleAsync(string roleName)
    {
        return await _context.users
            .AsNoTracking()
            .Where(x =>
                x.role == roleName &&
                !x.is_deleted)
            .OrderBy(x => x.full_name)
            .ToListAsync();
    }

    public async Task<IEnumerable<user>> GetActiveOperatorsAsync()
    {
        return await _context.users
            .AsNoTracking()
            .Where(x =>
                x.role == "operator" &&
                x.is_active &&
                !x.is_deleted)
            .OrderBy(x => x.full_name)
            .ToListAsync();
    }

    public async Task<bool> ChangeStatusAsync(int userId, bool isActive)
    {
        var user = await _context.users
            .FirstOrDefaultAsync(x =>
                x.id == userId &&
                !x.is_deleted);

        if (user == null)
            return false;

        user.is_active = isActive;
        user.updated_at = DateTime.Now;

        return true;
    }

    /// <summary>Registra el acceso correcto y restablece el contador de intentos de forma inmediata.</summary>
    /// <param name="userId">Cuenta autenticada correctamente.</param>
    /// <param name="now">Instante UTC para comprobar si existe un bloqueo vigente.</param>
    /// <returns>Verdadero si el acceso se registró sin un bloqueo concurrente.</returns>
    public async Task<bool> UpdateLastLoginAsync(int userId, DateTime now)
    {
        // El filtro y la actualización ocurren en una sola sentencia: otro intento no puede bloquear entre ambos.
        int updated = await _context.users.Where(x => x.id == userId && x.is_active && !x.is_deleted
                && (x.locked_until == null || x.locked_until <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.last_login, DateTime.Now)
                .SetProperty(x => x.login_attempts, 0)
                .SetProperty(x => x.locked_until, (DateTime?)null));
        return updated == 1;
    }

    /// <summary>Incrementa el contador en SQL para que dos terminales no pierdan fallos simultáneos.</summary>
    /// <param name="userId">Identificador de la cuenta que falló.</param>
    /// <param name="now">Instante UTC del intento.</param>
    /// <param name="maximumAttempts">Número de fallos que activa el bloqueo.</param>
    /// <param name="lockDuration">Duración del bloqueo temporal.</param>
    public async Task RecordFailedLoginAsync(int userId, DateTime now, int maximumAttempts, TimeSpan lockDuration)
    {
        DateTime until = now.Add(lockDuration);
        // Al vencer un bloqueo se inicia un conteo nuevo; el quinto fallo queda visible como 5.
        await _context.users
            .Where(x => x.id == userId && x.is_active && !x.is_deleted
                && (x.locked_until == null || x.locked_until <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.locked_until,
                    x => x.locked_until != null && x.locked_until <= now
                        ? (DateTime?)null
                        : x.login_attempts >= maximumAttempts - 1 ? until : x.locked_until)
                .SetProperty(x => x.login_attempts,
                    x => x.locked_until != null && x.locked_until <= now
                        ? 1
                        : x.login_attempts >= maximumAttempts ? maximumAttempts : x.login_attempts + 1));
    }

    public Task UpdateAsync(user entity)
    {
        _context.users.Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(long id)
    {
        var user = await _context.users
            .FirstOrDefaultAsync(x =>
                x.id == id &&
                !x.is_deleted);

        if (user == null)
            return;

        user.is_deleted = true;
        user.deleted_at = DateTime.Now;

        _context.users.Update(user);
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> ExistsByusernameAsync(string username)
    {
        return await _context.users
            .AnyAsync(x =>
                x.username == username &&
                !x.is_deleted);
    }

    public async Task<user?> GetByusernameAsync(string username)
    {
        return await _context.users
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.username == username &&
                !x.is_deleted);
    }

    public async Task<user?> GetByUsernameAsync(
      string username)
    {
        return await _context.users
            .FirstOrDefaultAsync(x =>
                x.username == username &&
                !x.is_deleted);
    }
}
