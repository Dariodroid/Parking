using Microsoft.EntityFrameworkCore;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;

namespace Parking.Infrastructure.DataAccess.Repository;

public class UserRepository : IBaseRepository<User>, IUserRepository
{
    private readonly parking_dbContext _context;

    public UserRepository(parking_dbContext context)
    {
        _context = context;
    }

    public async Task<User> AddAsync(User entity)
    {
        await _context.users.AddAsync(entity);
        return entity;
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.users
            .AsNoTracking()
            .Where(x => !x.is_deleted)
            .OrderBy(x => x.full_name)
            .Select(x => new User
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

    public async Task<User?> GetByIdAsync(long id)
    {
        return await _context.users
            .FirstOrDefaultAsync(x =>
                x.id == id &&
                !x.is_deleted);
    }

    public async Task<User?> GetByEmailAsync(string email)
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

    public async Task<IEnumerable<User>> GetUsersByRoleAsync(string roleName)
    {
        return await _context.users
            .AsNoTracking()
            .Where(x =>
                x.role == roleName &&
                !x.is_deleted)
            .OrderBy(x => x.full_name)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetActiveOperatorsAsync()
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

    public async Task UpdateLastLoginAsync(int userId)
    {
        var user = await _context.users
            .FirstOrDefaultAsync(x =>
                x.id == userId &&
                !x.is_deleted);

        if (user == null)
            return;

        user.last_login = DateTime.Now;
        user.login_attempts = 0;

        _context.users.Update(user);
    }

    public Task UpdateAsync(User entity)
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

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _context.users
            .AnyAsync(x =>
                x.username == username &&
                !x.is_deleted);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.users
            .FirstOrDefaultAsync(x =>
                x.username == username &&
                !x.is_deleted);
    }
}