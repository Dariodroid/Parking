using System.Windows;

namespace Parking.UI.Windows.View.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(Window owner, string message)
    {
        InitializeComponent();

        Owner = owner;

        Width = owner.ActualWidth;
        Height = owner.ActualHeight;

        Left = owner.Left;
        Top = owner.Top;

        MessageText.Text = message;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}