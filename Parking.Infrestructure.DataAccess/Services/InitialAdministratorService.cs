using Microsoft.EntityFrameworkCore;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Interfaces;
using Parking.Domain.Model.Models;
using Parking.Infrastructure.DataAccess;
using System.Data;

namespace Parking.Infrastructure.DataAccess.Services;

/// <summary>Crea la primera cuenta administrativa únicamente cuando la tabla de usuarios está vacía.</summary>
public sealed class InitialAdministratorService : IInitialAdministratorService
{
    private readonly parking_dbContext _database;
    private readonly IPasswordHasher _passwordHasher;

    /// <summary>Prepara el acceso a usuarios y al algoritmo de hash usado por el login.</summary>
    /// <param name="database">Contexto de la base configurada en este equipo.</param>
    /// <param name="passwordHasher">Generador PBKDF2 compartido con el registro normal.</param>
    public InitialAdministratorService(parking_dbContext database, IPasswordHasher passwordHasher)
    {
        _database = database;
        _passwordHasher = passwordHasher;
    }

    /// <summary>Comprueba si esta base nunca ha tenido una cuenta de usuario.</summary>
    /// <returns>Verdadero solo cuando la tabla users está completamente vacía.</returns>
    public async Task<bool> IsRequiredAsync() => !await _database.users.AnyAsync();

    /// <summary>Guarda el primer administrador con una contraseña nueva y hash aleatorio.</summary>
    /// <param name="username">Identificador único de hasta 50 caracteres.</param>
    /// <param name="fullName">Nombre visible de hasta 120 caracteres.</param>
    /// <param name="password">Contraseña elegida por el instalador.</param>
    /// <returns>Verdadero si se creó la cuenta; falso si otra cuenta ya existía.</returns>
    public async Task<bool> CreateAsync(string username, string fullName, string password)
    {
        // Se validan los límites de la tabla antes de intentar la escritura.
        username = username.Trim();
        fullName = fullName.Trim();
        if (username.Length is < 3 or > 50 || fullName.Length is < 3 or > 120 || password.Length < 8)
            throw new ArgumentException("Revise el usuario, nombre y contraseña.");

        // La transacción evita que dos instalaciones creen simultáneamente la primera cuenta.
        await using var transaction = await _database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await _database.users.AnyAsync())
            return false;

        // Se usa el mismo PBKDF2 con sal aleatoria que emplea la pantalla normal de usuarios.
        _database.users.Add(new user
        {
            username = username,
            full_name = fullName,
            password_hash = _passwordHasher.HashPassword(password),
            role = "Administrador",
            is_active = true,
            is_deleted = false,
            login_attempts = 0,
            created_at = DateTime.Now
        });
        await _database.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }
}
