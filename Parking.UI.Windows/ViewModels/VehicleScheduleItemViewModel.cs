using Parking.UI.Windows.ViewModels.Base;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class VehicleScheduleItemViewModel : BaseViewModel
{
    private static readonly TimeSpan FullDayEndTime = new(23, 59, 59);
    private bool _isEnabled;
    private TimeSpan _manualStartTime;
    private TimeSpan _manualEndTime;

    /// <summary>Indica si el día permite entradas; al desactivarlo retira la marca 24H.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value)) return;
            // Un día inactivo no debe conservar una opción 24H que no se aplica.
            if (!value && IsFullDay) IsFullDay = false;
            OnPropertyChanged(nameof(CanEditHours));
        }
    }

    private bool _isFullDay;

    /// <summary>Presenta el día completo y bloquea la edición de horas mientras está marcado.</summary>
    public bool IsFullDay
    {
        get => _isFullDay;
        set
        {
            // La jornada completa solo puede configurarse para un día activo.
            if (value && !IsEnabled) return;
            if (_isFullDay == value) return;
            // Al activar 24H se conservan las horas manuales para poder recuperarlas.
            if (value)
            {
                _manualStartTime = StartTime;
                _manualEndTime = EndTime;
            }
            SetProperty(ref _isFullDay, value);
            // TimeOnly no admite 24:00; 23:59:59 representa el último segundo del día.
            StartTime = value ? TimeSpan.Zero : _manualStartTime;
            EndTime = value ? FullDayEndTime : _manualEndTime;
            OnPropertyChanged(nameof(CanEditHours));
        }
    }

    /// <summary>Habilita campos y flechas únicamente en días activos con horario manual.</summary>
    public bool CanEditHours => IsEnabled && !IsFullDay;

    private TimeSpan _startTime;

    /// <summary>Hora de entrada; se fija en medianoche cuando el día es 24H.</summary>
    public TimeSpan StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, IsFullDay ? TimeSpan.Zero : value);
    }

    private TimeSpan _endTime;

    /// <summary>Hora de salida; se fija al último segundo cuando el día es 24H.</summary>
    public TimeSpan EndTime
    {
        get => _endTime;
        set => SetProperty(ref _endTime, IsFullDay ? FullDayEndTime : value);
    }

    public int DayOfWeek { get; set; }

    public string DayName { get; set; } = string.Empty;

    public ICommand IncreaseStartHourCommand { get; }

    public ICommand DecreaseStartHourCommand { get; }

    public ICommand IncreaseEndHourCommand { get; }

    public ICommand DecreaseEndHourCommand { get; }

    /// <summary>Prepara los comandos de ajuste horario de este día.</summary>
    public VehicleScheduleItemViewModel()
    {
        IncreaseStartHourCommand = new RelayCommand(_ => ChangeStartHour(1));
        DecreaseStartHourCommand = new RelayCommand(_ => ChangeStartHour(-1));
        IncreaseEndHourCommand = new RelayCommand(_ => ChangeEndHour(1));
        DecreaseEndHourCommand = new RelayCommand(_ => ChangeEndHour(-1));
    }

    /// <summary>Ajusta la entrada una hora en la dirección indicada.</summary>
    private void ChangeStartHour(int hours)
    {
        if (!CanEditHours) return;
        StartTime = WrapHour(StartTime.Add(TimeSpan.FromHours(hours)));
    }

    /// <summary>Ajusta la salida una hora en la dirección indicada.</summary>
    private void ChangeEndHour(int hours)
    {
        if (!CanEditHours) return;
        EndTime = WrapHour(EndTime.Add(TimeSpan.FromHours(hours)));
    }

    /// <summary>Vuelve a medianoche al pasar de 23 horas y a 23 horas al retroceder desde cero.</summary>
    private static TimeSpan WrapHour(TimeSpan value)
    {
        if (value.TotalHours >= 24) return TimeSpan.Zero;
        if (value.TotalHours < 0) return new TimeSpan(23, 0, 0);
        return value;
    }
}
