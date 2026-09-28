using Parking.UI.Windows.ViewModels;
using System.Windows;

namespace Parking.UI.Windows.View.Pages;

/// <summary>Permite configurar SQL en el primer arranque, antes del login.</summary>
public partial class ConnectionSetupWindow : Window
{
    private readonly ApplicationSettingsViewModel _viewModel;

    /// <summary>Abre la misma pantalla de configuración usada dentro de la aplicación.</summary>
    /// <param name="viewModel">Preferencias editables de esta ventana.</param>
    public ConnectionSetupWindow(ApplicationSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        // Guardar cierra el asistente inicial para continuar con el login.
        _viewModel.ConnectionSaved += OnConnectionSaved;
        Closed += OnClosed;
    }

    /// <summary>Cierra el asistente cuando la conexión se ha guardado.</summary>
    /// <param name="sender">Modelo que terminó de persistir.</param>
    /// <param name="e">Datos del aviso.</param>
    private void OnConnectionSaved(object? sender, EventArgs e) => DialogResult = true;

    /// <summary>Retira suscripciones al cerrar la ventana.</summary>
    /// <param name="sender">Ventana cerrada.</param>
    /// <param name="e">Datos del cierre.</param>
    private void OnClosed(object? sender, EventArgs e) => _viewModel.ConnectionSaved -= OnConnectionSaved;
}
