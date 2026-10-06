using System.Security.Cryptography;
using System.Text.Json;

namespace Parking.Infrastructure.CrossCutting.Licensing;

/// <summary>Comprueba la firma del proveedor antes de aceptar una licencia local.</summary>
public sealed class LicenseVerifier
{
    /// <summary>Lee la clave pública incluida en el ejecutable; la privada nunca se distribuye.</summary>
    public static string GetPublicKeyPem()
    {
        using Stream stream = typeof(LicenseVerifier).Assembly.GetManifestResourceStream(
            "Parking.Licensing.PublicKey") ?? throw new InvalidOperationException("Falta la clave pública de licencias.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Valida firma, formato y vinculación con la instalación actual.</summary>
    public bool TryVerify(string? token, string installationCode, out LicensePayload? license, out string error)
    {
        license = null;
        error = "La licencia no es válida.";
        LicensePayload? candidate = ReadSignedLicense(token);
        if (candidate is null || candidate.Version != 1 || string.IsNullOrWhiteSpace(candidate.LicenseId)
            || string.IsNullOrWhiteSpace(candidate.Customer))
            return false;

        if (!string.Equals(candidate.InstallationCode, installationCode, StringComparison.Ordinal))
        {
            error = "Esta licencia pertenece a otro equipo. Contacte al proveedor para activar este equipo.";
            return false;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (candidate.IssuedAtUtc > now.AddHours(1))
        {
            error = "La fecha u hora de Windows parece incorrecta. Corríjala y vuelva a abrir el sistema.";
            return false;
        }

        if (candidate.ExpiresAtUtc is { } expiration && now >= expiration)
        {
            error = "La licencia venció. Contacte al proveedor para renovarla.";
            return false;
        }

        license = candidate;
        error = string.Empty;
        return true;
    }

    /// <summary>Devuelve el contenido solo después de comprobar la firma digital.</summary>
    private static LicensePayload? ReadSignedLicense(string? token)
    {
        if (!LicenseTokenCodec.TryRead(token, out byte[] document, out byte[] signature)) return null;

        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(GetPublicKeyPem());
            if (!key.VerifyData(document, signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) return null;

            return JsonSerializer.Deserialize<LicensePayload>(document);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
