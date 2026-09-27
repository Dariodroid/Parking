namespace Parking.UI.Windows.View.Dialogs;

/// <summary>Combinaciones de acciones que puede ofrecer la ventana de mensaje.</summary>
public enum DialogButtons
{
    /// <summary>Una única acción para aceptar el mensaje.</summary>
    OK,
    /// <summary>Aceptar o cancelar el mensaje.</summary>
    OKCancel,
    /// <summary>Responder sí o no.</summary>
    YesNo,
    /// <summary>Responder sí, no o cancelar.</summary>
    YesNoCancel,
    /// <summary>Confirmar una eliminación o rechazarla.</summary>
    DeleteCancel
}
