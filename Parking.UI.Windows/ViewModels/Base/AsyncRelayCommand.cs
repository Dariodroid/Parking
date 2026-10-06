using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels.Base;

/// <summary>Comando asíncrono que se deshabilita mientras se ejecuta su tarea.</summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isExecuting;

    /// <summary>Crea un comando que espera una operación asíncrona.</summary>
    /// <param name="execute">Función obligatoria que recibe el parámetro y devuelve su tarea.</param>
    /// <param name="canExecute">Condición adicional opcional para habilitar el comando.</param>
    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        // Se exige una función ejecutable y se conserva la condición si existe.
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>Permite ejecutar cuando no hay otra tarea activa y se cumple la condición opcional.</summary>
    /// <param name="parameter">Dato proporcionado por el enlace de WPF.</param>
    /// <returns>Verdadero si se puede iniciar la operación.</returns>
    public bool CanExecute(object? parameter)
        => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    /// <summary>Ejecuta la tarea y restablece el comando al finalizar, incluso ante un error.</summary>
    /// <param name="parameter">Dato proporcionado por el enlace de WPF.</param>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        // ICommand exige void; la operación real se espera para mantener el estado ocupado.
        _isExecuting = true;
        RaiseCanExecuteChanged();
        try { await _execute(parameter); }
        finally
        {
            // Un error también debe liberar el comando para una nueva operación.
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>Reenvía los cambios de disponibilidad gestionados por WPF.</summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>Solicita a WPF que vuelva a consultar <see cref="CanExecute"/>.</summary>
    private void RaiseCanExecuteChanged()
        => CommandManager.InvalidateRequerySuggested();
}
