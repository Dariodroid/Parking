using Parking.Domain.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Abstractions
{
    public interface IuserRepository : IBaseRepository<user>
    {
        // --- Autenticación y Seguridad ---

        // Para el Login: Busca al usuario por su credencial única
        Task<user?> GetByEmailAsync(string email);

        // Para verificar si el usuario existe antes de registrarlo
        Task<bool> ExistsByEmailAsync(string email);

        // --- Gestión de Parqueo y Roles ---

        // Para obtener usuarios según su rol (ej. 'Admin', 'Operador', 'Cliente')
        Task<IEnumerable<user>> GetusersByRoleAsync(string roleName);

        // Para saber qué operario está activo en un turno de parqueo
        Task<IEnumerable<user>> GetActiveOperatorsAsync();

        // --- Auditoría y Estado ---

        // En un sistema de parqueo no conviene borrar usuarios, sino desactivarlos
        // para mantener el historial de tickets y cobros.
        Task<bool> ChangeStatusAsync(int userId, bool isActive);

        // Registrar el último acceso (útil para auditoría de seguridad)
        Task UpdateLastLoginAsync(int userId);

        Task<bool> ExistsByusernameAsync(string username);

        Task<user?> GetByusernameAsync(string username);
    }
}
