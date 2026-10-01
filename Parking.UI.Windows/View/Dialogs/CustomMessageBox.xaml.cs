using System.Windows;

namespace Parking.UI.Windows.View.Dialogs;

/// <summary>Ventana modal que muestra un mensaje y los botones indicados por el llamador.</summary>
public partial class CustomMessageBox : Window
{
    /// <summary>Construye el diálogo sobre la ventana propietaria con contenido y acciones elegidas.</summary>
    /// <param name="owner">Ventana visible que define posición y tamaño; nula durante el arranque.</param>
    /// <param name="title">Título visible del mensaje.</param>
    /// <param name="message">Texto principal presentado al usuario.</param>
    /// <param name="buttons">Combinación de botones que se mostrará.</param>
    /// <param name="icon">Símbolo del mensaje; Información si se omite.</param>
    public CustomMessageBox(Window? owner, string title, string message, DialogButtons buttons, DialogIcon icon = DialogIcon.Info)
    {
        // Carga los controles definidos en XAML antes de asignarles valores.
        InitializeComponent();

        // Superpone el diálogo a la ventana de origen usando sus dimensiones.
        if (owner is { IsVisible: true })
        {
            Owner = owner;
            Width = owner.ActualWidth;
            Height = owner.ActualHeight;
            Left = owner.Left;
            Top = owner.Top;
        }
        else
        {
            // Antes del login todavía no hay una ventana propietaria.
            Width = 520;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        // El contenido y las acciones proceden de los argumentos recibidos.
        TitleText.Text = title;
        MessageText.Text = message;
        SetIcon(icon);
        ConfigureButtons(buttons);
    }

    /// <summary>Asigna el símbolo correspondiente al tipo de mensaje.</summary>
    /// <param name="icon">Categoría visual que eligió el llamador.</param>
    private void SetIcon(DialogIcon icon)
    {
        // Cada categoría tiene un símbolo; la opción predeterminada es Información.
        IconText.Text = icon switch
        {
            DialogIcon.Info => "ℹ️",
            DialogIcon.Success => "✅",
            DialogIcon.Warning => "⚠️",
            DialogIcon.Error => "❌",
            DialogIcon.Question => "❓",
            _ => "ℹ️"
        };
    }

    /// <summary>Oculta todos los botones y habilita solo la combinación solicitada.</summary>
    /// <param name="buttons">Conjunto de acciones permitidas en el diálogo.</param>
    private void ConfigureButtons(DialogButtons buttons)
    {
        // Primero ocultamos todos
        BtnOK.Visibility = Visibility.Collapsed;
        BtnYes.Visibility = Visibility.Collapsed;
        BtnNo.Visibility = Visibility.Collapsed;
        BtnCancel.Visibility = Visibility.Collapsed;
        BtnDelete.Visibility = Visibility.Collapsed; // 🟢 Reset del nuevo botón

        // Luego mostramos solo los necesarios
        switch (buttons)
        {
            case DialogButtons.OK:
                BtnOK.Visibility = Visibility.Visible;
                break;
            case DialogButtons.OKCancel:
                BtnOK.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
                break;
            case DialogButtons.YesNo:
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
                break;
            case DialogButtons.YesNoCancel:
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
                break;
            case DialogButtons.DeleteCancel: // 🟢 Nuevo caso para eliminaciones
                BtnDelete.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible; // "No" actúa como "Cancelar"
                break;
        }
    }

    /// <summary>Confirma y cierra el diálogo al pulsar Aceptar.</summary>
    /// <param name="sender">Botón Aceptar.</param>
    /// <param name="e">Datos del clic.</param>
    private void BtnOK_Click(object sender, RoutedEventArgs e)
    {
        // El resultado verdadero equivale a aceptar el mensaje.
        DialogResult = true;
        Close();
    }
    /// <summary>Confirma y cierra el diálogo al pulsar Sí.</summary>
    /// <param name="sender">Botón Sí.</param>
    /// <param name="e">Datos del clic.</param>
    private void BtnYes_Click(object sender, RoutedEventArgs e)
    {
        // Sí confirma la acción consultada por el llamador.
        DialogResult = true;
        Close();
    }
    /// <summary>Rechaza y cierra el diálogo al pulsar No.</summary>
    /// <param name="sender">Botón No.</param>
    /// <param name="e">Datos del clic.</param>
    private void BtnNo_Click(object sender, RoutedEventArgs e)
    {
        // No rechaza la acción consultada por el llamador.
        DialogResult = false;
        Close();
    }
    /// <summary>Cancela y cierra el diálogo.</summary>
    /// <param name="sender">Botón Cancelar.</param>
    /// <param name="e">Datos del clic.</param>
    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        // Cancelar devuelve el mismo resultado negativo sin ejecutar la acción.
        DialogResult = false;
        Close();
    }

    /// <summary>Confirma la eliminación solicitada y cierra el diálogo.</summary>
    /// <param name="sender">Botón Eliminar.</param>
    /// <param name="e">Datos del clic.</param>
    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        // Un resultado verdadero informa al llamador que la eliminación fue aceptada.
        DialogResult = true;
        Close();
    }
}
