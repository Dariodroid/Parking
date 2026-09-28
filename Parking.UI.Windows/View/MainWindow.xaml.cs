using Parking.UI.Windows.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Parking.UI.Windows.View
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _cameraShutdownComplete;
        private bool _cameraShutdownInProgress;

        /// <summary>Inicializa la ventana y evita que al maximizar tape el pie de las páginas.</summary>
        /// <param name="viewModel">Modelo de navegación que proporciona la vista actual.</param>
        public MainWindow(MainWindowViewModel viewModel)
        {
            // Carga los controles declarados en MainWindow.xaml.
            InitializeComponent();
            // Las vistas internas reciben el contexto de navegación.
            DataContext = viewModel;
            // El cierre espera la liberación del driver; navegar entre vistas no la provoca.
            Closing += MainWindow_Closing;

            // WindowChrome con WindowStyle=None puede maximizar unos píxeles
            // por debajo del área utilizable y ocultar el pie de las vistas.
            // El ancho y la altura máximos respetan el área libre de la pantalla.
            MaxWidth = SystemParameters.WorkArea.Width;
            MaxHeight = SystemParameters.WorkArea.Height;
        }

        /// <summary>Detiene las cámaras al salir de la aplicación y espera su liberación.</summary>
        /// <param name="sender">Ventana principal que se está cerrando.</param>
        /// <param name="e">Permite aplazar el cierre hasta terminar la captura.</param>
        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_cameraShutdownComplete) return;

            // Una segunda solicitud de cierre no inicia otro StopCameraAsync concurrente.
            e.Cancel = true;
            if (_cameraShutdownInProgress) return;
            _cameraShutdownInProgress = true;
            try
            {
                if (DataContext is MainWindowViewModel viewModel)
                    await viewModel.ShutdownCamerasAsync();
            }
            catch (Exception ex)
            {
                // Un fallo de un controlador nativo no impide cerrar la aplicación.
                System.Diagnostics.Debug.WriteLine($"Error al liberar cámaras: {ex}");
            }
            finally
            {
                // Después de liberar las fuentes, la siguiente llamada cierra la ventana.
                _cameraShutdownComplete = true;
                Close();
            }
        }

        private void MenuToggle_Click(object sender, RoutedEventArgs e)
        {
            // Si hay algún desplegable abierto, lo cerramos y forzamos el menú a 100
            if (ExpanderReportes.IsExpanded || ExpanderConfig.IsExpanded)
            {
                ExpanderReportes.IsExpanded = false;
                ExpanderConfig.IsExpanded = false;
                MenuToggle.IsChecked = false;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
