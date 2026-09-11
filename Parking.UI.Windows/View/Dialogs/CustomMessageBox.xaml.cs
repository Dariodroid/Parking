using System.Windows;

namespace Parking.UI.Windows.View.Dialogs;

public partial class CustomMessageBox : Window
{
    public CustomMessageBox(Window owner, string title, string message, DialogButtons buttons, DialogIcon icon = DialogIcon.Info)
    {
        InitializeComponent();

        Owner = owner;
        Width = owner.ActualWidth;
        Height = owner.ActualHeight;
        Left = owner.Left;
        Top = owner.Top;

        TitleText.Text = title;
        MessageText.Text = message;
        SetIcon(icon);
        ConfigureButtons(buttons);
    }

    private void SetIcon(DialogIcon icon)
    {
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

    private void BtnOK_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void BtnYes_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void BtnNo_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void BtnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    // 🟢 Nuevo manejador para el botón Eliminar
    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true; // True porque el usuario confirmó la acción de eliminar
        Close();
    }
}

// 🟢 Enum actualizado con la nueva opción
public enum DialogButtons { OK, OKCancel, YesNo, YesNoCancel, DeleteCancel }
public enum DialogIcon { Info, Success, Warning, Error, Question }