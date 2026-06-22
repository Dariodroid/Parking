using ClosedXML.Excel;
using Parking.Domain.Model.Models;

namespace Parking.UI.Windows.Services;

public class ExcelExportService
{
    public void ExportPayments(
        IEnumerable<payment> payments,
        string filePath)
    {
        using var workbook = new XLWorkbook();

        var ws = workbook.Worksheets.Add("Caja");

        ws.Cell(1, 1).Value = "Placa";
        ws.Cell(1, 2).Value = "Operador";
        ws.Cell(1, 3).Value = "Monto";
        ws.Cell(1, 4).Value = "Método";
        ws.Cell(1, 5).Value = "Referencia";
        ws.Cell(1, 6).Value = "Fecha";
        ws.Cell(1, 7).Value = "Observación";

        int row = 2;

        foreach (var p in payments)
        {
            ws.Cell(row, 1).Value = p.session?.plate;
            ws.Cell(row, 2).Value = p.collected_byNavigation?.full_name;
            ws.Cell(row, 3).Value = p.amount_paid;
            ws.Cell(row, 4).Value = p.payment_method;
            ws.Cell(row, 5).Value = p.payment_reference;
            ws.Cell(row, 6).Value = p.collected_at;
            ws.Cell(row, 7).Value = p.notes;

            row++;
        }

        ws.Columns().AdjustToContents();

        workbook.SaveAs(filePath);
    }
}