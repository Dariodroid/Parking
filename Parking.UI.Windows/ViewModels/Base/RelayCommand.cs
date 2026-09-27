using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels.Base
{
    /// <summary>
    /// RelayCommand síncrono. Para async usa AsyncRelayCommand.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        /// <summary>Crea un comando que ejecuta una acción síncrona.</summary>
        /// <param name="execute">Acción obligatoria que recibe el parámetro del comando.</param>
        /// <param name="canExecute">Condición opcional para habilitar el comando.</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            // La acción debe existir; la condición puede omitirse para habilitar siempre.
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>Consulta la condición de habilitación, o permite ejecutar si no se proporcionó.</summary>
        /// <param name="parameter">Dato proporcionado por el enlace de WPF.</param>
        /// <returns>Verdadero cuando el comando puede ejecutarse.</returns>
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        /// <summary>Invoca la acción asociada al comando.</summary>
        /// <param name="parameter">Dato proporcionado por el enlace de WPF.</param>
        public void Execute(object? parameter) => _execute(parameter);

        /// <summary>Reenvía el cambio de disponibilidad gestionado por CommandManager.</summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>Solicita a WPF que vuelva a evaluar la disponibilidad del comando.</summary>
        public void RaiseCanExecuteChanged()
            => CommandManager.InvalidateRequerySuggested();
    }
}
