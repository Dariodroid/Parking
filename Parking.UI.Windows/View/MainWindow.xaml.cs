using Parking.UI.Windows.ViewModels;
using System.Windows;

namespace Parking.UI.Windows.View
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
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