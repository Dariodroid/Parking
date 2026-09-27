using Microsoft.Win32;
using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class VehicleReportViewModel : BaseViewModel
{
    private readonly IVehicleReportRepository _repository;
    private readonly ReportExportService _exportService = new();
    private ObservableCollection<VehicleReportDto> _vehicles = new();
    private IReadOnlyList<VehicleReportRow> _reportRows = [];
    private decimal _totalCollected;
    private DateTime _reportDate = DateTime.Now;
    private VehicleReportFilterDto _appliedFilter = new();

    public ObservableCollection<VehicleReportDto> Vehicles
    {
        get => _vehicles;
        private set => SetProperty(ref _vehicles, value);
    }
    public IReadOnlyList<VehicleReportRow> ReportRows
    {
        get => _reportRows;
        private set => SetProperty(ref _reportRows, value);
    }
    public VehicleReportFilterDto Filter { get; } = new()
    {
        IncludeMonthly = true,
        IncludeOccasional = true,
        IncludeInside = true,
        IncludeOutside = true,
        FromDate = DateTime.Today.AddMonths(-1),
        ToDate = DateTime.Today
    };
    public VehicleReportFilterDto AppliedFilter
    {
        get => _appliedFilter;
        private set
        {
            if (!SetProperty(ref _appliedFilter, value)) return;
            OnPropertyChanged(nameof(AppliedPeriod));
            OnPropertyChanged(nameof(AppliedCriteria));
        }
    }
    public string AppliedPeriod => ReportExportService.VehiclePeriod(AppliedFilter);
    public string AppliedCriteria => ReportExportService.VehicleCriteria(AppliedFilter);
    public decimal TotalCollected
    {
        get => _totalCollected;
        private set => SetProperty(ref _totalCollected, value);
    }
    public DateTime ReportDate
    {
        get => _reportDate;
        private set => SetProperty(ref _reportDate, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand ExportWordCommand { get; }

    public VehicleReportViewModel(IVehicleReportRepository repository)
    {
        _repository = repository;
        SearchCommand = new AsyncRelayCommand(async _ => await LoadData());
        ExportExcelCommand = new AsyncRelayCommand(async _ => await Export(false));
        ExportWordCommand = new AsyncRelayCommand(async _ => await Export(true));
    }

    public async Task LoadData()
    {
        if (Filter.FromDate > Filter.ToDate)
        {
            MessageBox.Show("La fecha inicial debe ser anterior o igual a la fecha final.", "Reportes");
            return;
        }

        var snapshot = new VehicleReportFilterDto
        {
            Plate = Filter.Plate,
            OwnerName = Filter.OwnerName,
            IncludeMonthly = Filter.IncludeMonthly,
            IncludeOccasional = Filter.IncludeOccasional,
            IncludeInside = Filter.IncludeInside,
            IncludeOutside = Filter.IncludeOutside,
            FromDate = Filter.FromDate,
            ToDate = Filter.ToDate,
            VehicleTypeId = Filter.VehicleTypeId
        };
        var data = await _repository.GetReportAsync(snapshot);
        Vehicles = new ObservableCollection<VehicleReportDto>(data);
        ReportRows = data.Select((vehicle, index) => new VehicleReportRow(index + 1, vehicle)).ToList();
        TotalCollected = data.Sum(x => x.TotalCollected);
        AppliedFilter = snapshot;
        ReportDate = DateTime.Now;
    }

    private async Task Export(bool word)
    {
        if (Filter.FromDate > Filter.ToDate)
        {
            MessageBox.Show("La fecha inicial debe ser anterior o igual a la fecha final.", "Reportes");
            return;
        }
        if (!FiltersMatch(Filter, AppliedFilter))
            await LoadData();

        var dialog = new SaveFileDialog
        {
            Filter = word ? "Documento Word (*.docx)|*.docx" : "Libro Excel (*.xlsx)|*.xlsx",
            FileName = $"ReporteVehiculos_{DateTime.Now:yyyyMMdd_HHmmss}.{(word ? "docx" : "xlsx")}",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            if (word)
                _exportService.ExportVehiclesWord(dialog.FileName, Vehicles, AppliedFilter, ReportDate);
            else
                _exportService.ExportVehiclesExcel(dialog.FileName, Vehicles, AppliedFilter, ReportDate);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo exportar el informe: {ex.Message}", "Reportes", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool FiltersMatch(VehicleReportFilterDto current, VehicleReportFilterDto applied) =>
        string.Equals(current.Plate?.Trim(), applied.Plate?.Trim(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(current.OwnerName?.Trim(), applied.OwnerName?.Trim(), StringComparison.OrdinalIgnoreCase) &&
        current.IncludeMonthly == applied.IncludeMonthly &&
        current.IncludeOccasional == applied.IncludeOccasional &&
        current.IncludeInside == applied.IncludeInside &&
        current.IncludeOutside == applied.IncludeOutside &&
        current.FromDate == applied.FromDate &&
        current.ToDate == applied.ToDate &&
        current.VehicleTypeId == applied.VehicleTypeId;
}
