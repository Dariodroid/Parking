
using Parking.UI.Windows.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Parking.UI.Windows.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
        private void MenuToggle_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Si hay algún desplegable abierto, lo cerramos y forzamos el menú a 100
            if (ExpanderReportes.IsExpanded || ExpanderConfig.IsExpanded)
            {
                ExpanderReportes.IsExpanded = false;
                ExpanderConfig.IsExpanded = false;
                MenuToggle.IsChecked = false;
            }
        }
    }
}