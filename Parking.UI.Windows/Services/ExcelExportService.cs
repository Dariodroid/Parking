using ClosedXML.Excel;
using Parking.Application.Dto;

namespace Parking.UI.Windows.Services;

/// <summary>Da formato de informe profesional a las filas de cobros de caja.</summary>
public class ExcelExportService
{
    /// <summary>Guarda la lista de pagos recibida como informe XLSX de caja.</summary>
    /// <param name="source">Pagos previamente consultados para el período.</param>
    /// <param name="filePath">Ruta completa del archivo XLSX.</param>
    /// <param name="from">Fecha inicial mostrada en la cabecera.</param>
    /// <param name="to">Fecha final mostrada en la cabecera.</param>
    public void ExportPayments(IEnumerable<CashMovement> source, string filePath, DateTime from, DateTime to)
    {
        // Se reutiliza la misma instantánea para filas y total recaudado.
        var payments = source.ToList();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Caja");
        sheet.ShowGridLines = false;
        // Encabezado institucional, período y cifras generales.
        sheet.Cell("A1").Value = "SISTEMA DE GESTIÓN DE PARQUEADERO";
        sheet.Range("A1:H1").Merge();
        sheet.Range("A1:H1").Style.Fill.BackgroundColor = XLColor.FromHtml("#17324D");
        sheet.Range("A1:H1").Style.Font.FontColor = XLColor.White;
        sheet.Range("A1:H1").Style.Font.Bold = true;
        sheet.Row(1).Height = 28;
        sheet.Cell("A2").Value = "INFORME DE CAJA";
        sheet.Range("A2:H2").Merge();
        sheet.Range("A2:H2").Style.Font.FontColor = XLColor.FromHtml("#17324D");
        sheet.Range("A2:H2").Style.Font.Bold = true;
        sheet.Range("A2:H2").Style.Font.FontSize = 18;
        sheet.Row(2).Height = 34;
        sheet.Cell("A3").Value = $"Período: {from:dd/MM/yyyy} al {to:dd/MM/yyyy}";
        sheet.Cell("A4").Value = $"Emitido: {DateTime.Now:dd/MM/yyyy HH:mm}";
        sheet.Cell("A5").Value = "COBROS";
        sheet.Cell("B5").Value = payments.Count;
        sheet.Cell("C5").Value = "TOTAL RECAUDADO";
        sheet.Cell("D5").Value = payments.Sum(x => x.Amount);
        sheet.Cell("D5").Style.NumberFormat.Format = CurrencyDisplay.ExcelNumberFormat;
        sheet.Range("A5:H5").Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1F6");
        sheet.Range("A5:H5").Style.Font.Bold = true;

        // Las columnas conservan placa, cobrador y datos de cada pago.
        string[] headers = ["Placa", "Operador", "Monto", "Método", "Referencia", "Fecha de cobro", "Observación", "Origen"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(7, i + 1).Value = headers[i];
        sheet.Range("A7:H7").Style.Fill.BackgroundColor = XLColor.FromHtml("#245A81");
        sheet.Range("A7:H7").Style.Font.FontColor = XLColor.White;
        sheet.Range("A7:H7").Style.Font.Bold = true;
        sheet.Row(7).Height = 30;

        // Los cobros se escriben uno por fila con bandas alternadas.
        var row = 8;
        foreach (var p in payments)
        {
            sheet.Cell(row, 1).Value = p.Plate;
            sheet.Cell(row, 2).Value = p.OperatorName;
            sheet.Cell(row, 3).Value = p.Amount;
            sheet.Cell(row, 4).Value = p.PaymentMethod;
            sheet.Cell(row, 5).Value = p.Reference;
            sheet.Cell(row, 6).Value = p.CollectedAt;
            sheet.Cell(row, 7).Value = p.Notes;
            sheet.Cell(row, 8).Value = p.Origin;
            if (row % 2 == 0)
                sheet.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F8");
            row++;
        }
        // Una consulta vacía sigue produciendo un informe legible.
        if (payments.Count == 0)
        {
            sheet.Cell("A8").Value = "Sin cobros para el período seleccionado";
            sheet.Range("A8:H8").Merge();
        }

        // El total se coloca debajo de los pagos o del mensaje vacío.
        var totalRow = Math.Max(row, 9) + 1;
        sheet.Cell(totalRow, 2).Value = "TOTAL GENERAL";
        sheet.Cell(totalRow, 3).Value = payments.Sum(x => x.Amount);
        sheet.Range(totalRow, 1, totalRow, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1F6");
        sheet.Range(totalRow, 1, totalRow, 8).Style.Font.Bold = true;
        sheet.Range(8, 3, totalRow, 3).Style.NumberFormat.Format = CurrencyDisplay.ExcelNumberFormat;
        sheet.Range(8, 6, Math.Max(row - 1, 8), 6).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        sheet.Columns(1, 8).Width = 19;
        sheet.Column(2).Width = 30;
        sheet.Column(6).Width = 23;
        sheet.Column(7).Width = 42;
        // Filtros, cabecera fija y configuración de impresión facilitan revisión.
        sheet.Range(7, 1, Math.Max(row - 1, 7), 8).SetAutoFilter();
        sheet.SheetView.FreezeRows(7);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 7);
        workbook.SaveAs(filePath);
    }
}
