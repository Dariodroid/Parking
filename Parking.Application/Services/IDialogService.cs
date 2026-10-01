namespace Parking.Application.Services;

/// <summary>Puerto para mostrar mensajes con los diálogos de la interfaz.</summary>
public interface IDialogService
{
    bool ShowConfirmation(string title, string message);
    void ShowInfo(string title, string message);
    void ShowError(string title, string message);
    void ShowWarning(string title, string message);
    void ShowSuccess(string title, string message);
}
