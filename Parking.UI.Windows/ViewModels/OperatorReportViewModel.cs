using Microsoft.Win32;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class OperatorReportViewModel : BaseViewModel
{
    private readonly IOperatorReportRepository _repository;
    private readonly ReportExportService _exportService = new();
    private ObservableCollection<OperatorReportItem> _reportItems = new();
    private DateTime _fromDate = DateTime.Today.AddMonths(-1);
    private DateTime _toDate = DateTime.Today;
    private DateTime _appliedFromDate = DateTime.Today.AddMonths(-1);
    private DateTime _appliedToDate = DateTime.Today;
    private decimal _totalGeneral;
    private int _totalPayments;
    private DateTime _reportDate = DateTime.Now;

    public ObservableCollection<OperatorReportItem> ReportItems
    {
        get => _reportItems;
        private set => SetProperty(ref _reportItems, value);
    }
    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }
    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }
    public DateTime AppliedFromDate
    {
        get => _appliedFromDate;
        private set => SetProperty(ref _appliedFromDate, value);
    }
    public DateTime AppliedToDate
    {
        get => _appliedToDate;
        private set => SetProperty(ref _appliedToDate, value);
    }
    public decimal TotalGeneral
    {
        get => _totalGeneral;
        private set => SetProperty(ref _totalGeneral, value);
    }
    public int TotalPayments
    {
        get => _totalPayments;
        private set => SetProperty(ref _totalPayments, value);
    }
    public DateTime ReportDate
    {
        get => _reportDate;
        private set => SetProperty(ref _reportDate, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand ExportWordCommand { get; }

    public OperatorReportViewModel(IOperatorReportRepository repository)
    {
        _repository = repository;
        RefreshCommand = new AsyncRelayCommand(async _ => await LoadReport());
        ExportExcelCommand = new RelayCommand(_ => Export(false));
        ExportWordCommand = new RelayCommand(_ => Export(true));
    }

    private async Task LoadReport()
    {
        if (FromDate.Date > ToDate.Date)
        {
            MessageBox.Show("La fecha inicial debe ser anterior o igual a la fecha final.", "Reportes");
            return;
        }

        var data = await _repository.GetReportAsync(FromDate.Date, ToDate.Date.AddDays(1));
        ReportItems = new ObservableCollection<OperatorReportItem>(data);
        AppliedFromDate = FromDate.Date;
        AppliedToDate = ToDate.Date;
        TotalGeneral = data.Sum(x => x.TotalAmount);
        TotalPayments = data.Sum(x => x.TotalPayments);
        ReportDate = DateTime.Now;
    }

    public Task LoadAsync() => LoadReport();

    private void Export(bool word)
    {
        var dialog = new SaveFileDialog
        {
            Filter = word ? "Documento Word (*.docx)|*.docx" : "Libro Excel (*.xlsx)|*.xlsx",
            FileName = $"ReporteOperadores_{DateTime.Now:yyyyMMdd_HHmmss}.{(word ? "docx" : "xlsx")}",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            if (word)
                _exportService.ExportOperatorsWord(dialog.FileName, ReportItems, AppliedFromDate, AppliedToDate, ReportDate);
            else
                _exportService.ExportOperatorsExcel(dialog.FileName, ReportItems, AppliedFromDate, AppliedToDate, ReportDate);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo exportar el informe: {ex.Message}", "Reportes", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
