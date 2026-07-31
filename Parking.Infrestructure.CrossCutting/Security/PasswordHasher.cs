using Parking.Domain.Model.Abstractions;
using System.Security.Cryptography;

namespace Parking.Infrastructure.CrossCutting.Security;


public class PasswordHasher : IPasswordHasher
{

    private const int SaltSize = 16;

    private const int HashSize = 32;

    private const int Iterations = 100000;


    private const char Separator = '.';



    public string HashPassword(
        string password)
    {

        byte[] salt =
            RandomNumberGenerator.GetBytes(
                SaltSize);



        using var pbkdf2 =
            new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA512);



        byte[] hash =
            pbkdf2.GetBytes(HashSize);



        return string.Join(
            Separator,
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));

    }





    public bool VerifyPassword(
        string password,
        string storedHash)
    {

        if (string.IsNullOrWhiteSpace(storedHash))
            return false;



        string[] parts =
            storedHash.Split(Separator);



        if (parts.Length != 3)
            return false;



        int iterations =
            int.Parse(parts[0]);



        byte[] salt =
            Convert.FromBase64String(parts[1]);



        byte[] expectedHash =
            Convert.FromBase64String(parts[2]);



        using var pbkdf2 =
            new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA512);



        byte[] actualHash =
            pbkdf2.GetBytes(expectedHash.Length);



        return CryptographicOperations
            .FixedTimeEquals(
                actualHash,
                expectedHash);

    }

}