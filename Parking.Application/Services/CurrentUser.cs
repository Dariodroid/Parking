using Parking.Application.Dto;

namespace Parking.Application.Services;

public static class CurrentUser
{
    public static bool IsAuthenticated { get; private set; }

    public static UserDto? User { get; private set; }

    public static int Id => User?.Id ?? 0;

    public static string Username => User?.Username ?? string.Empty;

    public static string FullName => User?.FullName ?? string.Empty;

    public static string Role => User?.Role ?? string.Empty;

    public static void Login(UserDto user)
    {
        User = user;

        IsAuthenticated = true;
    }

    public static void Logout()
    {
        User = null;

        IsAuthenticated = false;
    }
}