using Parking.Infrastructure.CrossCutting.Licensing;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace Parking.LicenseIssuer;

/// <summary>Atiende los comandos técnicos de emisión y comprobación de licencias.</summary>
internal static class LicenseCommandLine
{
    /// <summary>Ejecuta un comando y devuelve un código de salida para automatización.</summary>
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "generate-key")
            {
                GenerateKey(args[1], args[2]);
                return 0;
            }
            if (args.Length == 5 && args[0] == "issue")
            {
                Issue(args[1], args[2], args[3], args[4], null);
                return 0;
            }
            if (args.Length == 6 && args[0] == "issue")
            {
                DateTimeOffset expiration = DateTimeOffset.Parse(args[4], CultureInfo.InvariantCulture);
                Issue(args[1], args[2], args[3], args[5], expiration);
                return 0;
            }
            if (args.Length == 3 && args[0] == "verify")
            {
                string token = File.ReadAllText(args[1]);
                bool valid = new LicenseVerifier().TryVerify(token, args[2], out LicensePayload? license,
                    out string error);
                Console.WriteLine(valid ? $"Válida para {license!.Customer}." : error);
                return valid ? 0 : 1;
            }

            Console.Error.WriteLine("Uso: generate-key <privada.pem> <public-key.pem>");
            Console.Error.WriteLine("     issue <privada.pem> <cliente> <codigo-instalacion> <salida.license>");
            Console.Error.WriteLine("     issue <privada.pem> <cliente> <codigo-instalacion> <vencimiento-ISO> <salida.license>");
            Console.Error.WriteLine("     verify <licencia.license> <codigo-instalacion>");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    /// <summary>Crea el par de claves solo para la instalación del proveedor.</summary>
    private static void GenerateKey(string privatePath, string publicPath)
    {
        if (File.Exists(privatePath) || File.Exists(publicPath))
            throw new InvalidOperationException("Ya existe una clave en una de las rutas. No se reemplazó ninguna.");

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(privatePath))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(publicPath))!);
        File.WriteAllText(privatePath, key.ExportECPrivateKeyPem());
        File.WriteAllText(publicPath, key.ExportSubjectPublicKeyInfoPem());
        Console.WriteLine("Claves creadas. Guarde la privada fuera de Git y haga una copia de seguridad segura.");
    }

    /// <summary>Emite un serial desde la línea de comandos cuando se requiere automatización.</summary>
    private static void Issue(string privatePath, string customer, string installationCode,
        string outputPath, DateTimeOffset? expiration)
    {
        string serial = new LicenseGenerator().Generate(privatePath, customer, installationCode, expiration);
        File.WriteAllText(outputPath, serial);
        Console.WriteLine($"Licencia creada para {customer}: {Path.GetFullPath(outputPath)}");
    }
}
