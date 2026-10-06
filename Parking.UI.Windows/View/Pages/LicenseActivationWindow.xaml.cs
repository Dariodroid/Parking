using Parking.UI.Windows.Interfaces;
using Parking.UI.Windows.Services;
using System.Windows;

namespace Parking.UI.Windows.View.Pages;

/// <summary>Activa la instalación antes de abrir SQL, el primer administrador o el login.</summary>
public partial class LicenseActivationWindow : Window
{
    private readonly LicenseManager _licenses;
    private readonly IDialogService _dialogs;

    /// <summary>Presenta el código local y recibe una licencia firmada por el proveedor.</summary>
    public LicenseActivationWindow(LicenseManager licenses, IDialogService dialogs)
    {
        _licenses = licenses;
        _dialogs = dialogs;
        InitializeComponent();
        CodeText.Text = licenses.InstallationCode;
        Loaded += (_, _) => LicenseText.Focus();
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(CodeText.Text);
            _dialogs.ShowInfo("Licencia", "Código de instalación copiado.");
        }
        catch (Exception)
        {
            _dialogs.ShowWarning("Licencia", "No se pudo copiar el código. Puede seleccionarlo y copiarlo manualmente.");
        }
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_licenses.TryActivate(LicenseText.Text, out var license, out string error))
            {
                _dialogs.ShowWarning("Licencia", error);
                return;
            }

            string period = license!.ExpiresAtUtc is { } expiration
                ? $"Válida hasta el {expiration.ToLocalTime():dd/MM/yyyy HH:mm}."
                : "Sin vencimiento.";
            _dialogs.ShowSuccess("Licencia activada", $"Serial aceptado para {license.Customer}. {period}");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error activando licencia: {ex}");
            _dialogs.ShowError("Licencia", "No se pudo guardar la licencia en esta cuenta de Windows.");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
