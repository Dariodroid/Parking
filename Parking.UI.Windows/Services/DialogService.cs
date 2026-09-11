using System.Windows;
using Parking.Application.Services;
using Parking.UI.Windows.View.Dialogs;

namespace Parking.UI.Windows.Services;

public class DialogService : IDialogService
{
    public bool ShowConfirmation(string title, string message)
    {
        var owner = System.Windows.Application.Current.MainWindow;
        var dialog = new CustomMessageBox(owner, title, message, DialogButtons.YesNo, DialogIcon.Question);
        return dialog.ShowDialog() == true;
    }

    public void ShowInfo(string title, string message)
    {
        var owner = System.Windows.Application.Current.MainWindow;
        var dialog = new CustomMessageBox(owner, title, message, DialogButtons.OK, DialogIcon.Info);
        dialog.ShowDialog();
    }

    public void ShowError(string title, string message)
    {
        var owner = System.Windows.Application.Current.MainWindow;
        var dialog = new CustomMessageBox(owner, title, message, DialogButtons.OK, DialogIcon.Error);
        dialog.ShowDialog();
    }

    public void ShowWarning(string title, string message)
    {
        var owner = System.Windows.Application.Current.MainWindow;
        var dialog = new CustomMessageBox(owner, title, message, DialogButtons.OK, DialogIcon.Warning);
        dialog.ShowDialog();
    }

    public void ShowSuccess(string title, string message)
    {
        var owner = System.Windows.Application.Current.MainWindow;
        var dialog = new CustomMessageBox(owner, title, message, DialogButtons.OK, DialogIcon.Success);
        dialog.ShowDialog();
    }
}