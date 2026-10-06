using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Interfaces;

namespace Parking.Application.Services;

/// <summary>Comprueba credenciales y coordina el bloqueo temporal de acceso.</summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private const string InvalidCredentialsMessage = "Usuario o contraseña incorrectos.";
    private const string LockedMessage =
        "Acceso bloqueado por cinco intentos fallidos. Espere hasta 15 minutos e inténtelo de nuevo.";
    private const int MaximumAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    // Mismo formato PBKDF2 que un usuario real: 100 000 iteraciones, sal y hash de 32 bytes.
    private const string DummyPasswordHash =
        "100000.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    /// <summary>Recibe el repositorio de cuentas y el verificador de contraseñas.</summary>
    public AuthenticationService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    /// <summary>Comprueba credenciales sin revelar si el usuario existe o está activo.</summary>
    /// <param name="request">Nombre de usuario y contraseña introducidos.</param>
    /// <returns>Usuario autenticado, rechazo genérico o aviso de bloqueo si la contraseña es válida.</returns>
    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        // La consulta no cambia el texto mostrado al usuario según su resultado.
        var user = await _userRepository.GetByUsernameAsync(request.Username);
        bool validPassword;
        try
        {
            // Un usuario inexistente también ejecuta PBKDF2 para reducir diferencias de tiempo.
            validPassword = _passwordHasher.VerifyPassword(
                request.Password, user?.password_hash ?? DummyPasswordHash);
        }
        catch (Exception ex) when (ex is FormatException ||
                                   ex is ArgumentException ||
                                   ex is OverflowException)
        {
            // Un hash dañado tampoco debe revelar que la cuenta sí existe.
            _passwordHasher.VerifyPassword(request.Password, DummyPasswordHash);
            validPassword = false;
        }

        // Solo una contraseña válida permite distinguir el bloqueo; una inválida mantiene el rechazo genérico.
        bool locked = user?.locked_until > DateTime.UtcNow;
        if (user == null || !user.is_active || !validPassword)
        {
            // Solo un usuario real y activo incrementa el contador; SQL hace la operación atómica.
            if (user != null && user.is_active && !locked && !validPassword)
                await _userRepository.RecordFailedLoginAsync(user.id, DateTime.UtcNow,
                    MaximumAttempts, LockDuration);
            return new LoginResult
            {
                Success = false,
                Message = InvalidCredentialsMessage
            };
        }

        // Quien conoce la contraseña correcta recibe una explicación útil del bloqueo temporal.
        if (locked)
            return new LoginResult { Success = false, Message = LockedMessage };

        var authenticatedUser = new UserDto
        {
            Id = user.id,
            Username = user.username,
            FullName = user.full_name,
            Role = user.role
        };

        // Una cuenta bloqueada por otro intento justo ahora tampoco puede iniciar sesión.
        if (!await _userRepository.UpdateLastLoginAsync(user.id, DateTime.UtcNow))
            return new LoginResult { Success = false, Message = LockedMessage };

        return new LoginResult
        {
            Success = true,
            Message = "Login correcto.",
            User = authenticatedUser
        };
    }

    /// <summary>Devuelve únicamente si las credenciales permiten iniciar sesión.</summary>
    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        var result = await LoginAsync(new LoginRequest
        {
            Username = username,
            Password = password
        });
        return result.Success;
    }
}
