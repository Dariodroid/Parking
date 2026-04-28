using System.Collections.Generic;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Abstractions
{
    public interface IBaseRepository<T> where T : class
    {
        // CREATE: Retorna la entidad para obtener el ID generado por SQL
        Task<T> AddAsync(T entity);

        // READ: Usamos long para compatibilidad con bigint
        Task<T?> GetByIdAsync(long id);

        Task<IEnumerable<T>> GetAllAsync();

        // UPDATE
        Task UpdateAsync(T entity);

        // DELETE
        Task DeleteAsync(long id);

        // PERSISTENCE
        Task<bool> SaveChangesAsync();
    }
}