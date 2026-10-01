using Parking.UI.Windows.Interfaces;
using System.Windows;
using System.Linq;
using Parking.UI.Windows.View.Dialogs;

namespace Parking.UI.Windows.Services;

public class DialogService : IDialogService
{
    /// <summary>Busca la ventana visible actual para superponer allí el aviso.</summary>
    /// <returns>Ventana activa o nulo durante el arranque.</returns>
    private static Window? GetOwner() => System.Windows.Application.Current.Windows
        .OfType<Window>().LastOrDefault(window => window.IsVisible && window.IsActive)
        ?? System.Windows.Application.Current.Windows.OfType<Window>()
            .LastOrDefault(window => window.IsVisible);

    public bool ShowConfirmation(string title, string message)
    {
        var owner = GetOwner();
        var dialog = new CustomMessageBox(owner, title, message, DialogButtons.YesNo, DialogIcon.Question);
        return dialog.ShowDialog() == true;
    }

    public void ShowInfo(string title, string message)
    {
        var owner = GetOwner();
        var dialog = new CustomMessageBox(owner, title, message, DialogButtons.OK, DialogIcon.Info);
        dialog.ShowDialog();
    }

    public void ShowError(string title, string message)
    {
        var owner = GetOwner();
        var dialog = new CustomMessageBox(owner, title, message, DialogButtons.OK, DialogIcon.Error);
        dialog.ShowDialog();
    }

    public void ShowWarning(string title, string message)
    {
        var owner = GetOwner();
        var dialog = new CustomMessageBox(owner, title, message, DialogButtons.OK, DialogIcon.Warning);
        dialog.ShowDialog();
    }

    public void ShowSuccess(string title, string message)
    {
        var owner = GetOwner();
        var dialog = new CustomMessageBox(owner, title, message, DialogButtons.OK, DialogIcon.Success);
        dialog.ShowDialog();
    }
}
