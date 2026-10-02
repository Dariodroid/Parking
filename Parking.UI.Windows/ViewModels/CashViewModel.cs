using Parking.UI.Windows.Interfaces;
using Microsoft.Win32;
using Parking.Application.Dto;
using Parking.Application.Services;
using Parking.Application.Interfaces;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
namespace Parking.UI.Windows.ViewModels;

public class CashViewModel : BaseViewModel
{
    private readonly ExcelExportService _excelExportService;
    private readonly IDialogService _dialogs;
    private string _status = string.Empty;

    /// <summary>Informa si alguna fuente de cobros no pudo cargarse.</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }
    private decimal _todayIncome;
    public decimal TodayIncome
    {
        get => _todayIncome;
        set => SetProperty(ref _todayIncome, value);
    }

    private decimal _monthIncome;
    public decimal MonthIncome
    {
        get => _monthIncome;
        set => SetProperty(ref _monthIncome, value);
    }

    private decimal _cashIncome;
    public decimal CashIncome
    {
        get => _cashIncome;
        set => SetProperty(ref _cashIncome, value);
    }

    private decimal _transferIncome;
    public decimal TransferIncome
    {
        get => _transferIncome;
        set => SetProperty(ref _transferIncome, value);
    }

    private decimal _cardIncome;
    public decimal CardIncome
    {
        get => _cardIncome;
        set => SetProperty(ref _cardIncome, value);
    }

    private int _totalPayments;
    public int TotalPayments
    {
        get => _totalPayments;
        set => SetProperty(ref _totalPayments, value);
    }

    private DateTime _fromDate = DateTime.Today;
    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    private DateTime _toDate = DateTime.Today;
    // Fechas del último resultado consultado; el Excel describe exactamente los pagos visibles.
    private DateTime _appliedFromDate = DateTime.Today;
    private DateTime _appliedToDate = DateTime.Today;
    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public ObservableCollection<CashMovement> Payments { get; }
        = new();

    private readonly ICashQueryService _cash;


    public ICommand RefreshCommand { get; }
    public ICommand ExportExcelCommand { get; }

    /// <summary>Prepara Caja para consultar salidas y cuotas mensuales y exportar la misma lista visible.</summary>
    /// <param name="cash">Consulta los cobros confirmados de salidas y mensualidades.</param>
    /// <param name="excelExportService">Genera el libro Excel con las filas mostradas.</param>
    /// <param name="dialogs">Muestra avisos de exportación con los diálogos del sistema.</param>
    public CashViewModel(ICashQueryService cash, ExcelExportService excelExportService,
        IDialogService dialogs)
    {
        _cash = cash;
        _excelExportService = excelExportService;
        _dialogs = dialogs;

        RefreshCommand =
            new AsyncRelayCommand(async _ =>
                await LoadAsync());
        ExportExcelCommand =
       new RelayCommand(_ => ExportExcel());

        _ = LoadAsync();
    }
    /// <summary>Consulta los pagos del rango seleccionado y recalcula las tarjetas de caja.</summary>
    /// <returns>Tarea que completa la carga de pagos y totales.</returns>
    private async Task LoadAsync()
    {
        try
        {
            // Primero se consultan ambas fuentes: nunca se muestra un total incompleto como si fuera final.
            DateTime from = FromDate.Date;
            DateTime to = ToDate.Date.AddDays(1);
            var payments = await _cash.GetMovementsAsync(from, to);

            Payments.Clear();
            foreach (var item in payments) Payments.Add(item);
            _appliedFromDate = FromDate;
            _appliedToDate = ToDate;
            TotalPayments = payments.Count;
            TodayIncome = payments.Where(x => x.CollectedAt.Date == DateTime.Today).Sum(x => x.Amount);
            MonthIncome = payments.Sum(x => x.Amount);
            CashIncome = payments.Where(x => x.PaymentMethod == "cash").Sum(x => x.Amount);
            TransferIncome = payments.Where(x => x.PaymentMethod == "transfer").Sum(x => x.Amount);
            CardIncome = payments.Where(x => x.PaymentMethod == "card").Sum(x => x.Amount);
            Status = string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Caja: {ex}");
            Status = "No se pudo cargar Caja completa. Revise la conexión y el libro de cuotas mensuales; no exporte hasta actualizar correctamente.";
            _dialogs.ShowError("Caja", Status);
            Payments.Clear();
            TodayIncome = MonthIncome = CashIncome = TransferIncome = CardIncome = 0;
            TotalPayments = 0;
        }
    }


/// <summary>Exporta los pagos actualmente mostrados con las fechas de su última consulta.</summary>
private void ExportExcel()
{
    if (!string.IsNullOrEmpty(Status))
    {
        _dialogs.ShowWarning("Caja", Status);
        return;
    }
    // El operador escoge la ubicación del archivo de Excel.
    SaveFileDialog dialog = new()
    {
        Filter = "Excel (*.xlsx)|*.xlsx",
        FileName =
            $"Caja_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
    };

    if (dialog.ShowDialog() != true)
        return;

    // Las fechas aplicadas evitan rotular el archivo con filtros editados pero aún no consultados.
    _excelExportService.ExportPayments(
        Payments,
        dialog.FileName,
        _appliedFromDate,
        _appliedToDate);

    _dialogs.ShowSuccess("Excel", "Archivo exportado correctamente.");
}
}
