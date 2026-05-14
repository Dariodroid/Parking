using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Lógica de interacción para vehicle_typePage.xaml
    /// </summary>
    public partial class vehicle_typePage : UserControl
    {
        public vehicle_typePage()
        {
            InitializeComponent();
        }
        private void HourlyRateTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.SelectAll();
                }));
            }
        }

        // SOLO ENTEROS
        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new("[^0-9]+");

            e.Handled = regex.IsMatch(e.Text);
        }

        // DECIMALES
        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;

            if (textBox == null)
            {
                e.Handled = true;
                return;
            }

            // Permitir números
            if (char.IsDigit(e.Text, 0))
            {
                e.Handled = false;
                return;
            }

            // Permitir SOLO un punto decimal
            if (e.Text == ".")
            {
                if (textBox.Text.Contains("."))
                {
                    e.Handled = true;
                    return;
                }

                e.Handled = false;
                return;
            }

            // Bloquear todo lo demás
            e.Handled = true;
        }
    }
}
