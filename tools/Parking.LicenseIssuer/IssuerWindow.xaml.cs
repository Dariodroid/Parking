using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Parking.LicenseIssuer;

/// <summary>Permite al proveedor emitir seriales sin usar comandos.</summary>
public partial class IssuerWindow : Window
{
    /// <summary>Busca la clave privada del proveedor en su carpeta habitual.</summary>
    public IssuerWindow()
    {
        InitializeComponent();
        PrivateKeyInput.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ParkingLicenses", "parking-private.pem");
        CustomerInput.Focus();
    }

    /// <summary>Habilita la fecha únicamente para un vencimiento personalizado.</summary>
    private void TermInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomDateInput is not null)
            CustomDateInput.IsEnabled = (TermInput.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "date";
    }

    /// <summary>Permite seleccionar la clave privada guardada fuera del proyecto.</summary>
    private void BrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Clave privada (*.pem)|*.pem" };
        if (dialog.ShowDialog(this) == true) PrivateKeyInput.Text = dialog.FileName;
    }

    /// <summary>Firma el serial para el código y plazo indicados.</summary>
    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        SerialOutput.Clear();
        try
        {
            if (!File.Exists(PrivateKeyInput.Text))
                throw new FileNotFoundException("No se encontró tu clave privada. Pulsa Buscar... para seleccionarla.");
            DateTimeOffset? expiration = GetExpiration();
            SerialOutput.Text = new LicenseGenerator().Generate(PrivateKeyInput.Text,
                CustomerInput.Text, CodeInput.Text, expiration);
            StatusText.Text = expiration is null
                ? "Serial sin vencimiento. Cópielo y envíelo al cliente."
                : $"Vence el {expiration.Value.ToLocalTime():dd/MM/yyyy HH:mm}. Copie y envíe el serial.";
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
                                  or System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            StatusText.Text = ex.Message;
        }
    }

    /// <summary>Convierte la duración seleccionada a una fecha final UTC o a licencia perpetua.</summary>
    private DateTimeOffset? GetExpiration()
    {
        string? term = (TermInput.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (term == "life") return null;
        if (term == "date")
        {
            if (CustomDateInput.SelectedDate is not { } date)
                throw new ArgumentException("Elija la fecha final de la licencia.");
            // La licencia permanece válida durante todo el día elegido.
            return new DateTimeOffset(DateTime.SpecifyKind(date.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local))
                .ToUniversalTime();
        }
        if (int.TryParse(term, out int months)) return DateTimeOffset.UtcNow.AddMonths(months);
        throw new ArgumentException("Elija la duración de la licencia.");
    }

    /// <summary>Copia únicamente el serial que debe recibir el cliente.</summary>
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SerialOutput.Text))
        {
            StatusText.Text = "Primero genere un serial.";
            return;
        }
        try
        {
            Clipboard.SetText(SerialOutput.Text);
            StatusText.Text = "Serial copiado. Envíe solamente este texto al cliente.";
        }
        catch (Exception)
        {
            StatusText.Text = "No se pudo copiar. Seleccione el serial y cópielo manualmente.";
        }
    }

    /// <summary>Guarda una copia opcional del serial emitido.</summary>
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SerialOutput.Text))
        {
            StatusText.Text = "Primero genere un serial.";
            return;
        }
        var dialog = new SaveFileDialog { Filter = "Licencia Parking (*.license)|*.license",
            FileName = "cliente.license" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, SerialOutput.Text);
            StatusText.Text = "Archivo guardado. Envíe solamente ese archivo al cliente.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = ex.Message;
        }
    }
}
