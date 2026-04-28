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
        public PlateReaderPage()
        {
            InitializeComponent();
            //DataContext = new PlateReaderViewModel();
        }
    }
}
