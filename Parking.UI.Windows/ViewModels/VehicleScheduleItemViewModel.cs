using Parking.UI.Windows.ViewModels.Base;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class VehicleScheduleItemViewModel : BaseViewModel
{
    private static readonly TimeSpan FullDayEndTime = new(23, 59, 59);
    private bool _isEnabled;
    private TimeSpan _manualStartTime;
    private TimeSpan _manualEndTime;

    /// <summary>Indica si el día permite entradas según su horario configurado.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value)) return;
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
        IncreaseStartHourCommand =
            new RelayCommand(_ => IncreaseStartHour());

        DecreaseStartHourCommand =
            new RelayCommand(_ => DecreaseStartHour());

        IncreaseEndHourCommand =
            new RelayCommand(_ => IncreaseEndHour());

        DecreaseEndHourCommand =
            new RelayCommand(_ => DecreaseEndHour());
    }

    /// <summary>Avanza una hora de entrada cuando se permite la edición.</summary>
    private void IncreaseStartHour()
    {
        if (!CanEditHours) return;
        StartTime = StartTime.Add(TimeSpan.FromHours(1));

        if (StartTime.TotalHours >= 24)
            StartTime = TimeSpan.Zero;
    }

    /// <summary>Retrocede una hora de entrada cuando se permite la edición.</summary>
    private void DecreaseStartHour()
    {
        if (!CanEditHours) return;
        StartTime = StartTime.Subtract(TimeSpan.FromHours(1));

        if (StartTime.TotalHours < 0)
            StartTime = new TimeSpan(23, 0, 0);
    }

    /// <summary>Avanza una hora de salida cuando se permite la edición.</summary>
    private void IncreaseEndHour()
    {
        if (!CanEditHours) return;
        EndTime = EndTime.Add(TimeSpan.FromHours(1));

        if (EndTime.TotalHours >= 24)
            EndTime = TimeSpan.Zero;
    }

    /// <summary>Retrocede una hora de salida cuando se permite la edición.</summary>
    private void DecreaseEndHour()
    {
        if (!CanEditHours) return;
        EndTime = EndTime.Subtract(TimeSpan.FromHours(1));

        if (EndTime.TotalHours < 0)
            EndTime = new TimeSpan(23, 0, 0);
    }
}
