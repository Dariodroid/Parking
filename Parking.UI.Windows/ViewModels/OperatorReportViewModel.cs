using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using ClosedXML.Excel;
using System.IO;

namespace Parking.UI.Windows.ViewModels;

public class OperatorReportViewModel : BaseViewModel
{
    private readonly IOperatorReportRepository _repository;

    private ObservableCollection<OperatorReportItem> _reportItems = new();

    public ObservableCollection<OperatorReportItem> ReportItems
    {
        get => _reportItems;
        set => SetProperty(ref _reportItems, value);
    }

    private DateTime _fromDate = DateTime.Today.AddMonths(-1);

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

    private decimal _totalGeneral;

    public decimal TotalGeneral
    {
        get => _totalGeneral;
        set => SetProperty(ref _totalGeneral, value);
    }

    public ICommand RefreshCommand { get; }

    public ICommand ExportExcelCommand { get; }

    public ICommand PrintCommand { get; }

    public ICommand ExportWordCommand { get; }

    public OperatorReportViewModel(
        IOperatorReportRepository repository)
    {
        _repository = repository;

        RefreshCommand =
            new RelayCommand(async _ => await LoadReport());

        ExportExcelCommand =
            new RelayCommand(async _ => await ExportExcel());

        PrintCommand =
            new RelayCommand(_ => Print());

        ExportWordCommand =
            new RelayCommand(async _ => await ExportWord());
    }

    private async Task LoadReport()
    {
        ReportItems.Clear();

        var data =
            await _repository.GetReportAsync(
                FromDate,
                ToDate.AddDays(1));

        foreach (var item in data)
            ReportItems.Add(item);

        ReportItems = new ObservableCollection<OperatorReportItem>(data);

        TotalGeneral = data.Sum(x => x.TotalAmount);
    }

    public async Task LoadAsync()
    {
        await LoadReport();
    }

    private async Task ExportExcel()
    {
        SaveFileDialog saveFile = new();

        saveFile.Filter =
            "Excel Workbook (*.xlsx)|*.xlsx";

        saveFile.FileName =
            $"ReporteOperadores_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        if (saveFile.ShowDialog() != true)
            return;

        using var workbook = new XLWorkbook();

        var ws = workbook.Worksheets.Add("Operadores");

        // =========================
        // TITULO
        // =========================
        ws.Cell("A1").Value = "REPORTE DE OPERADORES";
        ws.Range("A1:C1").Merge();

        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // =========================
        // FECHAS
        // =========================
        ws.Cell("A3").Value = $"Desde: {FromDate:dd/MM/yyyy}";
        ws.Cell("C3").Value = $"Hasta: {ToDate:dd/MM/yyyy}";

        // =========================
        // HEADERS
        // =========================
        ws.Cell("A5").Value = "Operador";
        ws.Cell("B5").Value = "Cantidad Cobros";
        ws.Cell("C5").Value = "Total Recaudado";

        ws.Range("A5:C5").Style.Font.Bold = true;
        ws.Range("A5:C5").Style.Fill.BackgroundColor =
            XLColor.LightGray;
        ws.Range("A5:C5").SetAutoFilter();

        ws.RangeUsed().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.RangeUsed().Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.SheetView.FreezeRows(5);

        // =========================
        // DATA
        // =========================
        int row = 6;

        foreach (var item in ReportItems)
        {
            ws.Cell(row, 1).Value = item.OperatorName;
            ws.Cell(row, 2).Value = item.TotalPayments;
            ws.Cell(row, 3).Value = item.TotalAmount;

            ws.Cell(row, 3).Style.NumberFormat.Format = "$ #,##0.00";

            row++;
        }

        // =========================
        // TOTAL GENERAL
        // =========================
        ws.Cell(row + 1, 2).Value = "TOTAL GENERAL:";
        ws.Cell(row + 1, 3).Value = TotalGeneral;

        ws.Cell(row + 1, 2).Style.Font.Bold = true;
        ws.Cell(row + 1, 3).Style.Font.Bold = true;
        ws.Cell(row + 1, 3).Style.NumberFormat.Format = "$ #,##0.00";

        // =========================
        // AJUSTE AUTOMÁTICO
        // =========================
        ws.Columns().AdjustToContents();

        // =========================
        // GUARDAR
        // =========================
        workbook.SaveAs(saveFile.FileName);
    }
    private async Task ExportWord()
    {
        SaveFileDialog saveFile = new();

        saveFile.Filter =
            "Documento Word (*.docx)|*.docx";

        saveFile.FileName =
            $"ReporteOperadores_{DateTime.Now:yyyyMMddHHmmss}.docx";

        if (saveFile.ShowDialog() != true)
            return;

        using WordprocessingDocument document =
            WordprocessingDocument.Create(
                saveFile.FileName,
                WordprocessingDocumentType.Document);

        MainDocumentPart mainPart =
            document.AddMainDocumentPart();

        mainPart.Document =
            new Document();

        Body body =
            new Body();

        // TITULO

        body.Append(
            new Paragraph(
                new Run(
                    new Text(
                        "REPORTE DE OPERADORES")))
            {
                ParagraphProperties =
                    new ParagraphProperties(
                        new Justification()
                        {
                            Val =
                            JustificationValues.Center
                        })
            });

        body.Append(
            new Paragraph(
                new Run(
                    new Text(""))));

        // FECHAS

        body.Append(
            new Paragraph(
                new Run(
                    new Text(
                        $"Desde: {FromDate:dd/MM/yyyy}"))));

        body.Append(
            new Paragraph(
                new Run(
                    new Text(
                        $"Hasta: {ToDate:dd/MM/yyyy}"))));

        body.Append(
            new Paragraph(
                new Run(
                    new Text(""))));

        // TABLA

        Table table =
            new Table();

        table.AppendChild(
            new TableProperties(
                new TableBorders(
                    new TopBorder
                    {
                        Val =
                        BorderValues.Single,
                        Size = 8
                    },
                    new BottomBorder
                    {
                        Val =
                        BorderValues.Single,
                        Size = 8
                    },
                    new LeftBorder
                    {
                        Val =
                        BorderValues.Single,
                        Size = 8
                    },
                    new RightBorder
                    {
                        Val =
                        BorderValues.Single,
                        Size = 8
                    },
                    new InsideHorizontalBorder
                    {
                        Val =
                        BorderValues.Single,
                        Size = 8
                    },
                    new InsideVerticalBorder
                    {
                        Val =
                        BorderValues.Single,
                        Size = 8
                    }
                )));

        // CABECERA

        TableRow header =
            new TableRow();

        header.Append(
            CreateCell("Operador"));

        header.Append(
            CreateCell("Cobros"));

        header.Append(
            CreateCell("Total Recaudado"));

        table.Append(header);

        // DATOS

        foreach (var item in ReportItems)
        {
            TableRow row =
                new TableRow();

            row.Append(
                CreateCell(item.OperatorName));

            row.Append(
                CreateCell(
                    item.TotalPayments.ToString()));

            row.Append(
                CreateCell(
                    item.TotalAmount.ToString("C")));

            table.Append(row);
        }

        body.Append(table);

        body.Append(
            new Paragraph(
                new Run(
                    new Text(""))));

        body.Append(
            new Paragraph(
                new Run(
                    new Text(
                        $"TOTAL GENERAL: {TotalGeneral:C}"))));

        mainPart.Document.Append(body);

        mainPart.Document.Save();
    }
    private static TableCell CreateCell(
    string value)
    {
        return new TableCell(
            new Paragraph(
                new Run(
                    new Text(value))));
    }

    private void Print()
    {
        // después implementamos impresión
    }
}