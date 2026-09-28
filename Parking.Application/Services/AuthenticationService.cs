using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.Domain.Model.Abstractions;


namespace Parking.Application.Services;


public class AuthenticationService : IAuthenticationService
{
    private const string InvalidCredentialsMessage = "Usuario o contraseña incorrectos.";
    // Mismo formato PBKDF2 que un usuario real: 100 000 iteraciones, sal y hash de 32 bytes.
    private const string DummyPasswordHash =
        "100000.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private readonly IuserRepository _userRepository;

    private readonly IPasswordHasher _passwordHasher;

    public AuthenticationService(IuserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;

        _passwordHasher = passwordHasher;
    }


    /// <summary>Comprueba credenciales sin revelar si el usuario existe o está activo.</summary>
    /// <param name="request">Nombre de usuario y contraseña introducidos.</param>
    /// <returns>Usuario autenticado o un mismo rechazo para todos los fallos de credenciales.</returns>
    public async Task<LoginResult> LoginAsync(
        LoginRequest request)
    {
        // La consulta no cambia el texto mostrado al usuario según su resultado.
        var user = await _userRepository.GetByusernameAsync(request.Username);
        bool validPassword;
        try
        {
            // Un usuario inexistente también ejecuta PBKDF2 para reducir diferencias de tiempo.
            validPassword = _passwordHasher.VerifyPassword(
                request.Password, user?.password_hash ?? DummyPasswordHash);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            // Un hash dañado tampoco debe revelar que la cuenta sí existe.
            _passwordHasher.VerifyPassword(request.Password, DummyPasswordHash);
            validPassword = false;
        }

        if (user == null || !user.is_active || !validPassword)
        {
            return new LoginResult
            {
                Success = false,
                Message = InvalidCredentialsMessage
            };
        }

        UserDto dto = new()
        {
            Id = user.id,

            Username = user.username,

            FullName = user.full_name,

            Role = user.role
        };

        await _userRepository.UpdateLastLoginAsync(user.id);

        return new LoginResult
        {
            Success = true,

            Message = "Login correcto.",

            User = dto
        };
    }

    public async Task<bool> AuthenticateAsync(
        string username,
        string password)
    {

        var result =
            await LoginAsync(
                new LoginRequest
                {
                    Username = username,

                    Password = password
                });


        return result.Success;

    }

}
