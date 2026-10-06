using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Parking.UI.Windows.View.Pages;

/// <summary>Muestra el formulario de tipos y tarifas de vehículo.</summary>
public partial class VehicleTypePage : UserControl
{
    public VehicleTypePage() => InitializeComponent();

    /// <summary>Selecciona el importe completo al entrar en el campo de tarifa.</summary>
    private void HourlyRateTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.Dispatcher.BeginInvoke(new Action(textBox.SelectAll));
    }

    /// <summary>Permite solamente los dígitos del cero al nueve.</summary>
    private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(character => character < '0' || character > '9');
    }

    /// <summary>Permite un dígito o un único punto decimal.</summary>
    private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            e.Handled = true;
            return;
        }

        if (e.Text.Length > 0 && char.IsDigit(e.Text[0]))
        {
            e.Handled = false;
            return;
        }

        e.Handled = e.Text != "." || textBox.Text.Contains('.');
    }
}
