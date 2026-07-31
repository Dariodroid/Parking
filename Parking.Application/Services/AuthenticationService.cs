using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.Domain.Model.Abstractions;


namespace Parking.Application.Services;


public class AuthenticationService : IAuthenticationService
{
    private readonly IuserRepository _userRepository;

    private readonly IPasswordHasher _passwordHasher;

    public AuthenticationService(
        IuserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;

        _passwordHasher = passwordHasher;
    }


    public async Task<LoginResult> LoginAsync(
        LoginRequest request)
    {


        var user =
            await _userRepository
            .GetByusernameAsync(request.Username);



        if (user == null)
        {

            return new LoginResult
            {
                Success = false,
                Message = "Usuario no encontrado."
            };

        }




        if (!user.is_active)
        {

            return new LoginResult
            {
                Success = false,
                Message = "Usuario desactivado."
            };

        }





        bool validPassword =
    _passwordHasher.VerifyPassword(
        request.Password,
        user.password_hash);



        if (!validPassword)
        {

            return new LoginResult
            {
                Success = false,
                Message = "Contraseña incorrecta."
            };

        }





        UserDto dto = new()
        {
            Id = user.id,

            Username = user.username,

            FullName = user.full_name,

            Role = user.role
        };





        await _userRepository
            .UpdateLastLoginAsync(user.id);





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