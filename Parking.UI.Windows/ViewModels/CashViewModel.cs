using Microsoft.Win32;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;


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
    private async Task LoadAsync()
    {
        Payments.Clear();

        var payments =
            await _cashRepository
                .GetPaymentsAsync(
                    FromDate.Date,
                    ToDate.Date.AddDays(1));

        foreach (var item in payments)
        {
            Payments.Add(item);
        }

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


private void ExportExcel()
{
    SaveFileDialog dialog = new()
    {
        Filter = "Excel (*.xlsx)|*.xlsx",
        FileName =
            $"Caja_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
    };

    if (dialog.ShowDialog() != true)
        return;

    _excelExportService.ExportPayments(
        Payments,
        dialog.FileName);

    MessageBox.Show(
        "Archivo exportado correctamente.",
        "Excel",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
}
}