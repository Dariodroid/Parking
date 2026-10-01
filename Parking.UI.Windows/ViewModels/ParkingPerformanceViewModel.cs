using Parking.UI.Windows.Interfaces;
using Microsoft.Win32;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Application.Services;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Windows.Input;
using System.Windows;

namespace Parking.UI.Windows.ViewModels;

/// <summary>Presenta el rendimiento diario y exporta exactamente la búsqueda aplicada.</summary>
public sealed class ParkingPerformanceViewModel : BaseViewModel
{
    private readonly IParkingPerformanceRepository _repository;
    private readonly IDialogService _dialogs;
    private readonly ReportExportService _export = new();
    private readonly ReportPrintService _printer = new();
    private DateTime _startDate = DateTime.Today.AddDays(-29);
    private DateTime _endDate = DateTime.Today;
    private DateTime _appliedStartDate = DateTime.Today.AddDays(-29);
    private DateTime _appliedEndDate = DateTime.Today;
    private ParkingPerformanceReport _report = new(0, [], DateTime.Now);
    private string _status = string.Empty;
    private bool _hasApplied;

    /// <summary>Fecha inicial editable de la búsqueda.</summary>
    public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }
    /// <summary>Fecha final editable de la búsqueda.</summary>
    public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }
    /// <summary>Inicio del período mostrado y exportado.</summary>
    public DateTime AppliedStartDate { get => _appliedStartDate; private set => SetProperty(ref _appliedStartDate, value); }
    /// <summary>Fin del período mostrado y exportado.</summary>
    public DateTime AppliedEndDate { get => _appliedEndDate; private set => SetProperty(ref _appliedEndDate, value); }
    /// <summary>Instantánea de los indicadores calculados.</summary>
    public ParkingPerformanceReport Report
    {
        get => _report;
        private set
        {
            if (!SetProperty(ref _report, value)) return;
            OnPropertyChanged(nameof(Days));
        }
    }
    /// <summary>Filas de la única tabla diaria en pantalla.</summary>
    public IReadOnlyList<DailyPerformanceRow> Days => Report.Days;
    /// <summary>Estado de la consulta o de la exportación.</summary>
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public ICommand RefreshCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand ExportWordCommand { get; }
    public ICommand PrintCommand { get; }

    /// <summary>Configura la consulta y las tres acciones del informe.</summary>
    /// <param name="repository">Fuente de sesiones, puestos y pagos.</param>
    /// <param name="dialogs">Muestra los resultados de las acciones en el diálogo del sistema.</param>
    public ParkingPerformanceViewModel(IParkingPerformanceRepository repository, IDialogService dialogs)
    {
        _repository = repository;
        _dialogs = dialogs;
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        ExportExcelCommand = new AsyncRelayCommand(_ => ExportAsync(false));
        ExportWordCommand = new AsyncRelayCommand(_ => ExportAsync(true));
        PrintCommand = new AsyncRelayCommand(sheet => PrintAsync(sheet as FrameworkElement));
    }

    /// <summary>Consulta hasta 93 días y conserva fechas aplicadas junto con las cifras.</summary>
    /// <returns>Tarea que actualiza la instantánea o explica el error.</returns>
    public async Task LoadAsync()
    {
        if (StartDate.Date > EndDate.Date || (EndDate.Date - StartDate.Date).TotalDays > 92)
        {
            Status = "Elija un período válido de hasta 93 días.";
            _dialogs.ShowWarning("Rendimiento", Status);
            return;
        }
        try
        {
            // Una sola instantánea alimenta tarjetas, tabla y exportaciones.
            var result = await _repository.GetAsync(StartDate.Date, EndDate.Date.AddDays(1), DateTime.Now);
            Report = result;
            AppliedStartDate = StartDate.Date;
            AppliedEndDate = EndDate.Date;
            _hasApplied = true;
            Status = result.Days.Count == 0 ? "Sin días en el período." : string.Empty;
        }
        catch (Exception ex)
        {
            Status = $"No se pudo consultar el rendimiento: {ex.Message}";
            _dialogs.ShowError("Rendimiento", Status);
        }
    }

    /// <summary>Exporta la instantánea visible después de aplicar cualquier cambio de fechas.</summary>
    /// <param name="word">Verdadero para Word; falso para Excel.</param>
    /// <returns>Tarea que termina al guardar o cancelar el archivo.</returns>
    private async Task ExportAsync(bool word)
    {
        if (!_hasApplied || AppliedStartDate != StartDate.Date || AppliedEndDate != EndDate.Date)
            await LoadAsync();
        if (!_hasApplied || AppliedStartDate != StartDate.Date || AppliedEndDate != EndDate.Date) return;
        var dialog = new SaveFileDialog
        {
            Filter = word ? "Documento Word (*.docx)|*.docx" : "Libro Excel (*.xlsx)|*.xlsx",
            FileName = $"RendimientoParqueadero_{DateTime.Now:yyyyMMdd_HHmmss}.{(word ? "docx" : "xlsx")}",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (word) _export.ExportPerformanceWord(dialog.FileName, Report, AppliedStartDate, AppliedEndDate);
            else _export.ExportPerformanceExcel(dialog.FileName, Report, AppliedStartDate, AppliedEndDate);
            Status = $"Informe guardado en {dialog.FileName}.";
            _dialogs.ShowSuccess("Rendimiento", Status);
        }
        catch (Exception ex)
        {
            Status = $"No se pudo exportar el informe: {ex.Message}";
            _dialogs.ShowError("Rendimiento", Status);
        }
    }

    /// <summary>Imprime el rendimiento después de aplicar cambios pendientes de período.</summary>
    /// <returns>Tarea que termina al cancelar o enviar el documento a Windows.</returns>
    /// <param name="sheet">Hoja visual del rendimiento diario.</param>
    private async Task PrintAsync(FrameworkElement? sheet)
    {
        // Se comprueba que el documento corresponda a las fechas que ve el operador.
        if (!_hasApplied || AppliedStartDate != StartDate.Date || AppliedEndDate != EndDate.Date)
            await LoadAsync();
        if (!_hasApplied || AppliedStartDate != StartDate.Date || AppliedEndDate != EndDate.Date) return;
        try
        {
            if (sheet is null) throw new InvalidOperationException("No se encontró la hoja del informe.");
            if (_printer.Print(sheet, "Ocupación y recaudación"))
            {
                Status = "Informe enviado a la impresora seleccionada.";
                _dialogs.ShowSuccess("Rendimiento", Status);
            }
        }
        catch (Exception ex)
        {
            Status = $"No se pudo imprimir el informe: {ex.Message}";
            _dialogs.ShowError("Rendimiento", Status);
        }
    }
}
