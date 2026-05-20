using Parking.UI.Windows.ViewModels.Base;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class VehicleScheduleItemViewModel : BaseViewModel
{
    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    private bool _isFullDay;

    public bool IsFullDay
    {
        get => _isFullDay;
        set => SetProperty(ref _isFullDay, value);
    }

    private TimeSpan _startTime;

    public TimeSpan StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, value);
    }

    private TimeSpan _endTime;

    public TimeSpan EndTime
    {
        get => _endTime;
        set => SetProperty(ref _endTime, value);
    }

    public int DayOfWeek { get; set; }

    public string DayName { get; set; } = string.Empty;

    public ICommand IncreaseStartHourCommand { get; }

    public ICommand DecreaseStartHourCommand { get; }

    public ICommand IncreaseEndHourCommand { get; }

    public ICommand DecreaseEndHourCommand { get; }

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

    private void IncreaseStartHour()
    {
        StartTime = StartTime.Add(TimeSpan.FromHours(1));

        if (StartTime.TotalHours >= 24)
            StartTime = TimeSpan.Zero;
    }

    private void DecreaseStartHour()
    {
        StartTime = StartTime.Subtract(TimeSpan.FromHours(1));

        if (StartTime.TotalHours < 0)
            StartTime = new TimeSpan(23, 0, 0);
    }

    private void IncreaseEndHour()
    {
        EndTime = EndTime.Add(TimeSpan.FromHours(1));

        if (EndTime.TotalHours >= 24)
            EndTime = TimeSpan.Zero;
    }

    private void DecreaseEndHour()
    {
        EndTime = EndTime.Subtract(TimeSpan.FromHours(1));

        if (EndTime.TotalHours < 0)
            EndTime = new TimeSpan(23, 0, 0);
    }
}