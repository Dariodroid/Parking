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
    /// Lógica de interacción para VehicleReportPage.xaml
    /// </summary>
    public partial class VehicleReportPage : UserControl
    {
        public VehicleReportPage()
        {
            InitializeComponent();
        }

        /// <summary>Desplaza la hoja completa cuando el puntero está sobre la tabla del informe.</summary>
        /// <param name="sender">Tabla que recibió la rueda del ratón.</param>
        /// <param name="e">Movimiento de la rueda y estado del evento.</param>
        private void ReportGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Delta indica la dirección; el visor exterior contiene toda la página.
            ReportScrollViewer.ScrollToVerticalOffset(
                ReportScrollViewer.VerticalOffset - e.Delta);
            // Evita que el DataGrid cree un desplazamiento interior independiente.
            e.Handled = true;
        }
    }
}
