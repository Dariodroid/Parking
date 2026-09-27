using Microsoft.Win32;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
namespace Parking.UI.Windows.ViewModels;

public class CashViewModel : BaseViewModel
{
    private readonly ExcelExportService _excelExportService;
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

    public ObservableCollection<payment> Payments { get; }
        = new();

    private readonly ICashRepository _cashRepository;


    public ICommand RefreshCommand { get; }
    public ICommand ExportExcelCommand { get; }

    public CashViewModel(ICashRepository cashRepository, ExcelExportService excelExportService)
    {
        _cashRepository = cashRepository;
        _excelExportService = excelExportService;

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
        // Se reemplaza el listado anterior antes de presentar el nuevo resultado.
        Payments.Clear();

        // El repositorio usa fin exclusivo; sumar un día incluye el último día seleccionado.
        var payments =
            await _cashRepository
                .GetPaymentsAsync(
                    FromDate.Date,
                    ToDate.Date.AddDays(1));

        foreach (var item in payments)
        {
            Payments.Add(item);
        }
        // Se guardan los criterios efectivos una vez cargados los datos.
        _appliedFromDate = FromDate;
        _appliedToDate = ToDate;

        TotalPayments =
            payments.Count;

        TodayIncome =
            payments
                .Where(x =>
                    x.collected_at.Date ==
                    DateTime.Today)
                .Sum(x => x.amount_paid);

        MonthIncome =
            payments.Sum(x => x.amount_paid);

        CashIncome =
            payments
                .Where(x =>
                    x.payment_method == "cash")
                .Sum(x => x.amount_paid);

        TransferIncome =
            payments
                .Where(x =>
                    x.payment_method == "transfer")
                .Sum(x => x.amount_paid);

        CardIncome =
            payments
                .Where(x =>
                    x.payment_method == "card")
                .Sum(x => x.amount_paid);
    }


/// <summary>Exporta los pagos actualmente mostrados con las fechas de su última consulta.</summary>
private void ExportExcel()
{
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

    MessageBox.Show(
        "Archivo exportado correctamente.",
        "Excel",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
}
}
