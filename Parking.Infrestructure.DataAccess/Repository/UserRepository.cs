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
        await _context.Users.AddAsync(entity);
        return entity;
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.FullName)
            .Select(x => new User
            {
                Id = x.Id,
                Username = x.Username,
                FullName = x.FullName,
                Role = x.Role,
                IsActive = x.IsActive,
                LastLogin = x.LastLogin,
                LoginAttempts = x.LoginAttempts,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<User?> GetByIdAsync(long id)
    {
        return await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                !x.IsDeleted);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Username == email &&
                !x.IsDeleted);
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        return await _context.Users
            .AnyAsync(x =>
                x.Username == email &&
                !x.IsDeleted);
    }

    public async Task<IEnumerable<User>> GetUsersByRoleAsync(string roleName)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.Role == roleName &&
                !x.IsDeleted)
            .OrderBy(x => x.FullName)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetActiveOperatorsAsync()
    {
        return await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.Role == "operator" &&
                x.IsActive &&
                !x.IsDeleted)
            .OrderBy(x => x.FullName)
            .ToListAsync();
    }

    public async Task<bool> ChangeStatusAsync(int userId, bool isActive)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                !x.IsDeleted);

        if (user == null)
            return false;

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.Now;

        return true;
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                !x.IsDeleted);

        if (user == null)
            return;

        user.LastLogin = DateTime.Now;
        user.LoginAttempts = 0;

        _context.Users.Update(user);
    }

    public Task UpdateAsync(User entity)
    {
        _context.Users.Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(long id)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                !x.IsDeleted);

        if (user == null)
            return;

        user.IsDeleted = true;
        user.DeletedAt = DateTime.Now;

        _context.Users.Update(user);
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _context.Users
            .AnyAsync(x =>
                x.Username == username &&
                !x.IsDeleted);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Username == username &&
                !x.IsDeleted);
    }
}