using Parking.UI.Windows.Services;
using Parking.Application.Services;
using System.Windows;

namespace Parking.UI.Windows.View.Pages;

/// <summary>Recoge las credenciales del primer administrador antes de permitir el login.</summary>
public partial class InitialAdministratorWindow : Window
{
    private readonly InitialAdministratorService _service;
    private readonly IDialogService _dialogs;

    /// <summary>Crea el asistente para una base sin cuentas.</summary>
    /// <param name="service">Servicio que verifica y guarda la primera cuenta.</param>
    /// <param name="dialogs">Muestra los avisos con el diálogo visual del sistema.</param>
    public InitialAdministratorWindow(InitialAdministratorService service, IDialogService dialogs)
    {
        _service = service;
        _dialogs = dialogs;
        InitializeComponent();
        Loaded += (_, _) => UsernameInput.Focus();
    }

    /// <summary>Valida los datos, crea la cuenta y deja continuar al login.</summary>
    /// <param name="sender">Botón de creación.</param>
    /// <param name="e">Argumentos del clic.</param>
    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        // El formulario rechaza contraseñas distintas antes de abrir una transacción SQL.
        string password = PasswordInput.Password;
        if (password != ConfirmPasswordInput.Password)
        {
            _dialogs.ShowWarning("Primera configuración", "Las contraseñas no coinciden.");
            return;
        }

        // El servicio repite las validaciones y comprueba que aún no exista ningún usuario.
        CreateButton.IsEnabled = false;
        StatusText.Text = string.Empty;
        try
        {
            bool created = await _service.CreateAsync(UsernameInput.Text, FullNameInput.Text, password);
            if (!created)
            {
                _dialogs.ShowWarning("Primera configuración", "Esta base ya tiene usuarios. Reinicie para iniciar sesión.");
                return;
            }

            // El siguiente paso es el login con las credenciales recién creadas.
            PasswordInput.Clear();
            ConfirmPasswordInput.Clear();
            DialogResult = true;
        }
        catch (ArgumentException)
        {
            _dialogs.ShowWarning("Primera configuración", "Use un usuario y nombre de 3 caracteres o más, y una contraseña de al menos 8.");
        }
        catch (Exception ex)
        {
            // El detalle SQL queda en depuración para no mostrar datos de conexión en pantalla.
            System.Diagnostics.Debug.WriteLine($"Error creando administrador inicial: {ex}");
            _dialogs.ShowError("Primera configuración", "No se pudo crear la cuenta. Revise la conexión y la estructura de la base de datos.");
        }
        finally
        {
            CreateButton.IsEnabled = true;
        }
    }
}
