namespace Parking.Infrastructure.CrossCutting.Licensing;

/// <summary>Convierte datos y firma en un texto transportable sin alterar los bytes firmados.</summary>
public static class LicenseTokenCodec
{
    /// <summary>Une el documento firmado y su firma para entregarlos al cliente.</summary>
    public static string Create(byte[] document, byte[] signature) =>
        Encode(document) + "." + Encode(signature);

    /// <summary>Separa un texto de licencia en sus bytes originales.</summary>
    public static bool TryRead(string? token, out byte[] document, out byte[] signature)
    {
        document = [];
        signature = [];
        if (string.IsNullOrWhiteSpace(token) || token.Length > 8192) return false;

        string[] parts = token.Trim().Split('.');
        if (parts.Length != 2) return false;
        try
        {
            document = Decode(parts[0]);
            signature = Decode(parts[1]);
            return document.Length > 0 && signature.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string text)
    {
        string value = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(value.PadRight((value.Length + 3) / 4 * 4, '='));
    }
}
