namespace Parking.LicenseIssuer;

/// <summary>Inicia la herramienta del proveedor en modo gráfico o por comandos.</summary>
internal static class Program
{
    /// <summary>Abre la ventana de emisión cuando no se proporcionan argumentos.</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0) return LicenseCommandLine.Run(args);

        new System.Windows.Application().Run(new IssuerWindow());
        return 0;
    }
}
