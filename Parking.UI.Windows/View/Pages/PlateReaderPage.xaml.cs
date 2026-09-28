using Parking.UI.Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Parking.UI.Windows.View.Pages
{
    /// <summary>
    /// Lógica de interacción para PlateReaderPage.xaml
    /// </summary>
    public partial class PlateReaderPage : UserControl
    {
        // Conserva el ViewModel para liberarlo aunque DataContext cambie al retirar la vista.
        private PlateReaderViewModel? _activeViewModel;

        /// <summary>Construye la vista de operaciones y sus dos paneles de cámara.</summary>
        public PlateReaderPage()
        {
            // Carga los controles y enlaces declarados en XAML.
            InitializeComponent();
        }

        /// <summary>Recuerda el controlador de capturas asociado a esta instancia de la vista.</summary>
        /// <param name="sender">Página que acaba de cargarse.</param>
        /// <param name="e">Datos del evento Loaded.</param>
        private void PlateReaderPage_Loaded(object sender, RoutedEventArgs e)
        {
            // El DataContext es heredado del DataTemplate de Operaciones.
            _activeViewModel = DataContext as PlateReaderViewModel;
        }

        /// <summary>Libera ambas fuentes al navegar fuera de la pantalla de operaciones.</summary>
        /// <param name="sender">Vista que sale del árbol visual.</param>
        /// <param name="e">Datos del evento Unloaded.</param>
        private async void PlateReaderPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // El ViewModel inicia el cierre para que cada VideoCapture suelte el driver.
            var viewModel = _activeViewModel ?? DataContext as PlateReaderViewModel;
            _activeViewModel = null;
            if (viewModel != null)
            {
                try { await viewModel.DeactivateAsync(); }
                catch (Exception)
                {
                    // Un driver defectuoso no debe impedir el cambio de pantalla.
                }
            }
        }

        /// <summary>Distribuye los dos visores en columnas anchas o en filas cuando falta espacio horizontal.</summary>
        /// <param name="sender">Página cuyo ancho visible cambió.</param>
        /// <param name="e">Tamaño anterior y nuevo de la página.</param>
        private void PlateReaderPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // El panel lateral ocupa ancho fijo; bajo este umbral cada cámara necesita todo el ancho restante.
            bool stacked = e.NewSize.Width < 1300;
            CameraLayoutGrid.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            CameraLayoutGrid.RowDefinitions[1].Height = stacked ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

            // En ventana estrecha la segunda fuente pasa bajo la primera.
            Grid.SetColumn(ExitCameraPanel, stacked ? 0 : 1);
            Grid.SetRow(ExitCameraPanel, stacked ? 1 : 0);
            EntranceCameraPanel.Margin = stacked ? new Thickness(0, 0, 0, 5) : new Thickness(0, 0, 5, 0);
            ExitCameraPanel.Margin = stacked ? new Thickness(0, 5, 0, 0) : new Thickness(5, 0, 0, 0);
        }
    }
}
