using Parking.Infrastructure.CrossCutting.Licensing;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Parking.LicenseIssuer;

/// <summary>Firma un serial válido solamente para el equipo y plazo elegidos.</summary>
public sealed class LicenseGenerator
{
    /// <summary>Genera el serial con la clave privada del proveedor.</summary>
    public string Generate(string privateKeyPath, string customer, string installationCode,
        DateTimeOffset? expiresAtUtc)
    {
        customer = customer.Trim();
        installationCode = installationCode.Trim().ToUpperInvariant();
        if (customer.Length < 2 || installationCode.Length != 32 ||
            !installationCode.All(Uri.IsHexDigit))
            throw new ArgumentException("Escriba el nombre del cliente y su código de equipo de 32 caracteres.");
        if (expiresAtUtc is { } expiration && expiration <= DateTimeOffset.UtcNow)
            throw new ArgumentException("La fecha de vencimiento debe ser posterior a hoy.");

        using var privateKey = ECDsa.Create();
        privateKey.ImportFromPem(File.ReadAllText(privateKeyPath));
        using var publicKey = ECDsa.Create();
        publicKey.ImportFromPem(LicenseVerifier.GetPublicKeyPem());
        if (!privateKey.ExportSubjectPublicKeyInfo().SequenceEqual(publicKey.ExportSubjectPublicKeyInfo()))
            throw new InvalidOperationException("La clave privada no corresponde a esta versión del sistema.");

        var license = new LicensePayload(1, Guid.NewGuid().ToString("N"), customer,
            installationCode, DateTimeOffset.UtcNow, expiresAtUtc?.ToUniversalTime());
        byte[] document = JsonSerializer.SerializeToUtf8Bytes(license);
        byte[] signature = privateKey.SignData(document, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return LicenseTokenCodec.Create(document, signature);
    }
}
