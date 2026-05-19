using Parking.UI.Windows.ViewModels.Base;
using System;

namespace Parking.UI.Windows.ViewModels
{
    public class VehicleScheduleItemViewModel : BaseViewModel
    {
        private int _dayOfWeek;

        public int DayOfWeek
        {
            get => _dayOfWeek;
            set => SetProperty(ref _dayOfWeek, value);
        }

        private string _dayName = string.Empty;

        public string DayName
        {
            get => _dayName;
            set => SetProperty(ref _dayName, value);
        }

        private bool _isEnabled;

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
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

        private bool _isFullDay;

        public bool IsFullDay
        {
            get => _isFullDay;
            set => SetProperty(ref _isFullDay, value);
        }
    }
}