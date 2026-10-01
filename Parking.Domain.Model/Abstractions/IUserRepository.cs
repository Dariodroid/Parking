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

        /// <summary>Registra el acceso correcto y limpia el bloqueo solo si ya no está vigente.</summary>
        /// <param name="userId">Cuenta cuya contraseña fue verificada.</param>
        /// <param name="now">Instante UTC para evaluar el bloqueo.</param>
        /// <returns>Verdadero si el acceso se registró; falso si la cuenta estaba bloqueada.</returns>
        Task<bool> UpdateLastLoginAsync(int userId, DateTime now);

        /// <summary>Cuenta un fallo de acceso y bloquea temporalmente la cuenta al alcanzar el límite.</summary>
        /// <param name="userId">Cuenta existente que falló la autenticación.</param>
        /// <param name="now">Instante UTC usado para calcular el fin del bloqueo.</param>
        /// <param name="maximumAttempts">Cantidad de fallos permitidos antes del bloqueo.</param>
        /// <param name="lockDuration">Tiempo que debe esperar el usuario tras alcanzar el límite.</param>
        Task RecordFailedLoginAsync(int userId, DateTime now, int maximumAttempts, TimeSpan lockDuration);

        Task<bool> ExistsByusernameAsync(string username);

        Task<user?> GetByusernameAsync(string username);

        Task<bool> SaveChangesAsync();
    }
}
