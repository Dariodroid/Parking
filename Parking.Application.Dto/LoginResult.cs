namespace Parking.Application.Dto;

public class LoginResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public UserDto? User { get; set; }
}