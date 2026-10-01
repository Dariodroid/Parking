using Microsoft.Win32;
using Parking.Application.Dto;
using Parking.Application.Interfaces;
using Parking.Application.Services;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

/// <summary>Coordina filtros, filas de vista previa y exportación de vehículos.</summary>
public class VehicleReportViewModel : BaseViewModel
{
    private readonly IVehicleReportRepository _repository;
    private readonly IDialogService _dialogs;
    private readonly ReportExportService _exportService = new();
    private readonly ReportPrintService _printService = new();
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
    /// <summary>Filas formateadas de la búsqueda aplicada; alimentan la única tabla visible.</summary>
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
    /// <summary>Copia del filtro con que se cargaron las filas visibles, independiente de los controles editables.</summary>
    public VehicleReportFilterDto AppliedFilter
    {
        get => _appliedFilter;
        private set
        {
            if (!SetProperty(ref _appliedFilter, value)) return;
            // El encabezado debe regenerarse al aplicar una nueva búsqueda.
            OnPropertyChanged(nameof(AppliedPeriod));
            OnPropertyChanged(nameof(AppliedCriteria));
        }
    }
    /// <summary>Período de la última búsqueda para el encabezado de la hoja.</summary>
    public string AppliedPeriod => ReportExportService.VehiclePeriod(AppliedFilter);
    /// <summary>Criterios de la última búsqueda para el encabezado de la hoja.</summary>
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
    public ICommand PrintCommand { get; }

    /// <summary>Configura búsqueda y exportaciones sobre el mismo repositorio.</summary>
    /// <param name="repository">Consulta vehículos según los filtros elegidos.</param>
    /// <param name="dialogs">Muestra advertencias y errores con los diálogos del sistema.</param>
    public VehicleReportViewModel(IVehicleReportRepository repository, IDialogService dialogs)
    {
        _repository = repository;
        _dialogs = dialogs;
        SearchCommand = new AsyncRelayCommand(async _ => await LoadData());
        ExportExcelCommand = new AsyncRelayCommand(async _ => await Export(false));
        ExportWordCommand = new AsyncRelayCommand(async _ => await Export(true));
        PrintCommand = new AsyncRelayCommand(async sheet => await Print(sheet as FrameworkElement));
    }

    /// <summary>Aplica los filtros, carga la tabla y conserva su instantánea.</summary>
    /// <returns>Tarea que termina al actualizar filas, resumen y fecha.</returns>
    public async Task LoadData()
    {
        // Se rechaza un período invertido antes de consultar.
        if (Filter.FromDate > Filter.ToDate)
        {
            _dialogs.ShowWarning("Reportes", "La fecha inicial debe ser anterior o igual a la fecha final.");
            return;
        }

        // Se copia el filtro: cambios posteriores en controles no alteran la
        // descripción ni el contenido de la búsqueda ya cargada.
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
        // Una consulta alimenta tabla, filas formateadas y total mostrado.
        var data = await _repository.GetReportAsync(snapshot);
        Vehicles = new ObservableCollection<VehicleReportDto>(data);
        ReportRows = data.Select((vehicle, index) => new VehicleReportRow(index + 1, vehicle)).ToList();
        TotalCollected = data.Sum(x => x.TotalCollected);
        AppliedFilter = snapshot;
        ReportDate = DateTime.Now;
    }

    /// <summary>Exporta la búsqueda actual a Word o Excel, refrescando si cambió el filtro.</summary>
    /// <param name="word">Verdadero para DOCX; falso para XLSX.</param>
    /// <returns>Tarea que termina tras guardar o mostrar un error.</returns>
    private async Task Export(bool word)
    {
        // No se exporta un intervalo de fechas inválido.
        if (Filter.FromDate > Filter.ToDate)
        {
            _dialogs.ShowWarning("Reportes", "La fecha inicial debe ser anterior o igual a la fecha final.");
            return;
        }
        // Si el usuario cambió criterios, se consulta antes de generar el archivo.
        if (!FiltersMatch(Filter, AppliedFilter))
            await LoadData();

        // El diálogo propone el nombre y extensión correspondientes.
        var dialog = new SaveFileDialog
        {
            Filter = word ? "Documento Word (*.docx)|*.docx" : "Libro Excel (*.xlsx)|*.xlsx",
            FileName = $"ReporteVehiculos_{DateTime.Now:yyyyMMdd_HHmmss}.{(word ? "docx" : "xlsx")}",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;

        // Word y Excel reciben exactamente Vehicles y AppliedFilter.
        try
        {
            if (word)
                _exportService.ExportVehiclesWord(dialog.FileName, Vehicles, AppliedFilter, ReportDate);
            else
                _exportService.ExportVehiclesExcel(dialog.FileName, Vehicles, AppliedFilter, ReportDate);
            _dialogs.ShowSuccess("Reportes", "Informe exportado correctamente.");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Reportes", $"No se pudo exportar el informe: {ex.Message}");
        }
    }

    /// <summary>Imprime las filas filtradas después de aplicar cambios pendientes en la búsqueda.</summary>
    /// <returns>Tarea que termina al cancelar o enviar el trabajo a Windows.</returns>
    /// <param name="sheet">Hoja visual del informe filtrado.</param>
    private async Task Print(FrameworkElement? sheet)
    {
        // Se usan los mismos criterios y filas que reciben Word y Excel.
        if (Filter.FromDate > Filter.ToDate)
        {
            _dialogs.ShowWarning("Reportes", "La fecha inicial debe ser anterior o igual a la fecha final.");
            return;
        }
        try
        {
            if (!FiltersMatch(Filter, AppliedFilter))
                await LoadData();
            if (sheet is null) throw new InvalidOperationException("No se encontró la hoja del informe.");
            _printService.Print(sheet, "Informe de vehículos");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Reportes", $"No se pudo imprimir el informe: {ex.Message}");
        }
    }

    /// <summary>Compara los controles actuales con el filtro de las filas visibles.</summary>
    /// <param name="current">Valores que el usuario puede estar editando.</param>
    /// <param name="applied">Copia del filtro usado en la última consulta.</param>
    /// <returns>Verdadero si exportar no requiere consultar de nuevo.</returns>
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
