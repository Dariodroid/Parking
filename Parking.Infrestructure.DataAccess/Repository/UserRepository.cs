using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Infrastructure.DataAccess.Repository
{
    public class UserRepository : IBaseRepository<User>, IUserRepository
    {
        public Task<User> AddAsync(User entity)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ChangeStatusAsync(int userId, bool isActive)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(long id)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsByEmailAsync(string email)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<User>> GetActiveOperatorsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<User>> GetAllAsync()
        {
            throw new NotImplementedException();
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            throw new NotImplementedException();
        }

        public Task<User?> GetByIdAsync(long id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<User>> GetUsersByRoleAsync(string roleName)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SaveChangesAsync()
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(User entity)
        {
            throw new NotImplementedException();
        }

        public Task UpdateLastLoginAsync(int userId)
        {
            throw new NotImplementedException();
        }
    }
}
