namespace Parking.Domain.Model.Abstractions;

public interface IPasswordHasher
{

    string HashPassword(
        string password);


    bool VerifyPassword(
        string password,
        string storedHash);

}