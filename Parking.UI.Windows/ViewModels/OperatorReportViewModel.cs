using Microsoft.Win32;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

/// <summary>Mantiene la búsqueda aplicada y la exportación del informe de operadores.</summary>
public class OperatorReportViewModel : BaseViewModel
{
    private readonly IOperatorReportRepository _repository;
    private readonly ReportExportService _exportService = new();
    private readonly ReportPrintService _printService = new();
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
    /// <summary>Inicio del período que produjo los resultados visibles.</summary>
    public DateTime AppliedFromDate
    {
        get => _appliedFromDate;
        private set => SetProperty(ref _appliedFromDate, value);
    }
    /// <summary>Fin del período que produjo los resultados visibles.</summary>
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
    public ICommand PrintCommand { get; }

    /// <summary>Conecta la consulta y los comandos de actualización/exportación.</summary>
    /// <param name="repository">Consulta cobros agrupados por operador.</param>
    public OperatorReportViewModel(IOperatorReportRepository repository)
    {
        _repository = repository;
        RefreshCommand = new AsyncRelayCommand(async _ => await LoadReport());
        ExportExcelCommand = new RelayCommand(_ => Export(false));
        ExportWordCommand = new RelayCommand(_ => Export(true));
        PrintCommand = new RelayCommand(sheet => Print(sheet as FrameworkElement));
    }

    /// <summary>Consulta el período elegido y conserva una instantánea para exportar.</summary>
    /// <returns>Tarea de carga de datos.</returns>
    private async Task LoadReport()
    {
        // El período inválido no se envía al repositorio.
        if (FromDate.Date > ToDate.Date)
        {
            MessageBox.Show("La fecha inicial debe ser anterior o igual a la fecha final.", "Reportes");
            return;
        }

        // El límite superior exclusivo incluye toda la fecha final.
        var data = await _repository.GetReportAsync(FromDate.Date, ToDate.Date.AddDays(1));
        // Se separan fechas aplicadas y editables para exportar lo mostrado.
        ReportItems = new ObservableCollection<OperatorReportItem>(data);
        AppliedFromDate = FromDate.Date;
        AppliedToDate = ToDate.Date;
        TotalGeneral = data.Sum(x => x.TotalAmount);
        TotalPayments = data.Sum(x => x.TotalPayments);
        ReportDate = DateTime.Now;
    }

    /// <summary>Carga el informe al abrir la vista.</summary>
    /// <returns>La misma tarea de consulta usada por Actualizar.</returns>
    public Task LoadAsync() => LoadReport();

    /// <summary>Exporta la instantánea visible a Word o Excel.</summary>
    /// <param name="word">Verdadero para DOCX; falso para XLSX.</param>
    private void Export(bool word)
    {
        // El diálogo usa extensión y filtro acordes al formato elegido.
        var dialog = new SaveFileDialog
        {
            Filter = word ? "Documento Word (*.docx)|*.docx" : "Libro Excel (*.xlsx)|*.xlsx",
            FileName = $"ReporteOperadores_{DateTime.Now:yyyyMMdd_HHmmss}.{(word ? "docx" : "xlsx")}",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;

        // Se entregan los resultados y fechas aplicados, no filtros aún editados.
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

    /// <summary>Envía las filas y fechas actualmente mostradas a la impresora Windows elegida.</summary>
    /// <param name="sheet">Hoja visual que muestra tarjetas, tabla y total.</param>
    private void Print(FrameworkElement? sheet)
    {
        // Los controles de fecha pueden estar editados; se imprime la última consulta visible.
        try
        {
            if (sheet is null) throw new InvalidOperationException("No se encontró la hoja del informe.");
            _printService.Print(sheet, "Informe de operadores");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo imprimir el informe: {ex.Message}", "Reportes",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
