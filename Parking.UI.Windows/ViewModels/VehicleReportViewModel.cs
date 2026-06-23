using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.UI.Windows.ViewModels.Base;
using ClosedXML.Excel;
using Microsoft.Win32;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class VehicleReportViewModel : BaseViewModel
{
    private readonly IVehicleReportRepository _repository;

    public ObservableCollection<VehicleReportDto> Vehicles
    {
        get;
        set;
    } = new();

    public VehicleReportFilterDto Filter
    {
        get;
        set;
    } = new()
    {
        FromDate = DateTime.Today.AddMonths(-1),
        ToDate = DateTime.Today
    };

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
            await _repository.GetReportAsync(
                Filter);

        foreach (var item in data)
        {
            Vehicles.Add(item);
        }
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
            "REPORTE DE VEHÍCULOS";

        ws.Range("A1:H1").Merge();

        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;

        ws.Cell("A3").Value =
            $"Desde: {Filter.FromDate:dd/MM/yyyy}";

        ws.Cell("D3").Value =
            $"Hasta: {Filter.ToDate:dd/MM/yyyy}";

        ws.Cell("A5").Value = "Placa";
        ws.Cell("B5").Value = "Propietario";
        ws.Cell("C5").Value = "Tipo";
        ws.Cell("D5").Value = "Categoría";
        ws.Cell("E5").Value = "Estado";
        ws.Cell("F5").Value = "Mensualidad";
        ws.Cell("G5").Value = "Ingresos";
        ws.Cell("H5").Value = "Último Ingreso";

        ws.Range("A5:H5").Style.Font.Bold = true;

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
                item.LastEntryDate?.ToString(
                    "dd/MM/yyyy HH:mm");

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

        MainDocumentPart mainPart =
            document.AddMainDocumentPart();

        mainPart.Document =
            new Document();

        Body body =
            new Body();

        body.Append(
            new Paragraph(
                new Run(
                    new Text(
                        "REPORTE DE VEHÍCULOS"))));

        Table table = new();

        TableRow header = new();

        header.Append(CreateCell("Placa"));
        header.Append(CreateCell("Propietario"));
        header.Append(CreateCell("Tipo"));
        header.Append(CreateCell("Categoría"));
        header.Append(CreateCell("Estado"));
        header.Append(CreateCell("Mensualidad"));
        header.Append(CreateCell("Ingresos"));

        table.Append(header);

        foreach (var item in Vehicles)
        {
            TableRow row = new();

            row.Append(CreateCell(item.Plate));
            row.Append(CreateCell(item.OwnerName));
            row.Append(CreateCell(item.VehicleType));
            row.Append(CreateCell(item.Category));
            row.Append(CreateCell(item.PlanStatus));
            row.Append(CreateCell(item.MonthlyFee.ToString("C")));
            row.Append(CreateCell(item.TotalEntries.ToString()));

            table.Append(row);
        }

        body.Append(table);

        mainPart.Document.Append(body);

        mainPart.Document.Save();
    }

    private TableCell CreateCell(
        string value)
    {
        return new TableCell(
            new Paragraph(
                new Run(
                    new Text(value))));
    }
}