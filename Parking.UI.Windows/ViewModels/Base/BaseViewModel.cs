using System.ComponentModel;
using System.Runtime.CompilerServices;

// NAMESPACE UNIFICADO — elimina Parking.UI.Windows.ViewModel.Base
// y Parking.UI.Windows.ViewModels.BaseViewModels. Solo este existe.
namespace Parking.UI.Windows.ViewModels.Base
{
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetProperty<T>(ref T field, T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}