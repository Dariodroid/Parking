using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.UI.Windows.ViewModels.Base;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class VehicleReportViewModel
    : BaseViewModel
{
    private readonly IVehicleReportRepository _repository;

    public ObservableCollection<VehicleReportDto>
        Vehicles
    { get; set; } = new();

    public VehicleReportFilterDto Filter
    {
        get;
        set;
    } = new()
    {
        IncludeMonthly = true,
        IncludeOccasional = true,
        FromDate = DateTime.Today.AddMonths(-1),
        ToDate = DateTime.Today
    };

    private decimal _totalCollected;

    public decimal TotalCollected
    {
        get => _totalCollected;
        set => SetProperty(
            ref _totalCollected,
            value);
    }

    public ICommand SearchCommand { get; }

    public ICommand ExportExcelCommand { get; }

    public ICommand ExportWordCommand { get; }

    public VehicleReportViewModel(
        IVehicleReportRepository repository)
    {
        _repository = repository;

        SearchCommand =
            new RelayCommand(
                async _ => await LoadData());

        ExportExcelCommand =
            new RelayCommand(
                async _ => await ExportExcel());

        ExportWordCommand =
            new RelayCommand(
                async _ => await ExportWord());
    }

    public async Task LoadData()
    {
        Vehicles.Clear();

        var data =
            await _repository
            .GetReportAsync(Filter);

        foreach (var item in data)
        {
            Vehicles.Add(item);
        }

        TotalCollected =
            data.Sum(x =>
                x.TotalCollected);
    }

    private async Task ExportExcel()
    {
        SaveFileDialog dialog = new();

        dialog.Filter =
            "Excel (*.xlsx)|*.xlsx";

        dialog.FileName =
            $"ReporteVehiculos_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

        if (dialog.ShowDialog() != true)
            return;

        using var workbook = new XLWorkbook();

        var ws =
            workbook.Worksheets.Add("Vehiculos");

        ws.Cell("A1").Value =
            "REPORTE DE VEHICULOS";

        ws.Range("A1:H1").Merge();

        ws.Cell("A5").Value = "Placa";
        ws.Cell("B5").Value = "Propietario";
        ws.Cell("C5").Value = "Tipo";
        ws.Cell("D5").Value = "Categoria";
        ws.Cell("E5").Value = "Estado";
        ws.Cell("F5").Value = "Mensualidad";
        ws.Cell("G5").Value = "Ingresos";
        ws.Cell("H5").Value = "Ultimo Ingreso";

        int row = 6;

        foreach (var item in Vehicles)
        {
            ws.Cell(row, 1).Value = item.Plate;
            ws.Cell(row, 2).Value = item.OwnerName;
            ws.Cell(row, 3).Value = item.VehicleType;
            ws.Cell(row, 4).Value = item.Category;
            ws.Cell(row, 5).Value = item.PlanStatus;
            ws.Cell(row, 6).Value = item.MonthlyFee;
            ws.Cell(row, 7).Value = item.TotalEntries;
            ws.Cell(row, 8).Value =
                item.LastEntryDate;

            row++;
        }

        ws.Columns().AdjustToContents();

        workbook.SaveAs(dialog.FileName);
    }

    private async Task ExportWord()
    {
        SaveFileDialog dialog = new();

        dialog.Filter =
            "Word (*.docx)|*.docx";

        dialog.FileName =
            $"ReporteVehiculos_{DateTime.Now:yyyyMMddHHmmss}.docx";

        if (dialog.ShowDialog() != true)
            return;

        using var document =
            WordprocessingDocument.Create(
                dialog.FileName,
                DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

        var mainPart =
            document.AddMainDocumentPart();

        mainPart.Document =
            new Document();

        var body =
            new Body();

        body.Append(
            new Paragraph(
                new Run(
                    new Text(
                        "REPORTE DE VEHICULOS"))));

        mainPart.Document.Append(body);

        mainPart.Document.Save();
    }
}