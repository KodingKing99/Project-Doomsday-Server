using ClosedXML.Excel;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Application.Files.Services;
using ProjectDoomsdayServer.Application.Interfaces;

namespace ProjectDoomsdayServer.Infrastructure.Files;

public sealed class ExcelExportService : IFileExportService
{
    public const string WorksheetName = "Expenses";
    public const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string DateNumberFormat = "yyyy-mm-dd";

    private readonly IFileService _fileService;

    public ExcelExportService(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<ExportFileResult> ExportFile(
        ExportFileRequest request,
        CancellationToken cancellationToken
    )
    {
        var file = await _fileService.GetAsync(
            new GetFileRequest
            {
                AuthenticatedUserId = request.AuthenticatedUserId,
                Id = request.FileId,
            },
            cancellationToken
        );

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(WorksheetName);

        WriteHeader(worksheet);
        var rows = ExpenseExportTable.BuildRows(file);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = rows[rowIndex];
            for (var columnIndex = 0; columnIndex < cells.Count; columnIndex++)
            {
                WriteCell(worksheet.Cell(rowIndex + 2, columnIndex + 1), cells[columnIndex]);
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ExportFileResult(stream.ToArray(), ExcelContentType, $"{request.FileId}.xlsx");
    }

    private static void WriteHeader(IXLWorksheet worksheet)
    {
        for (
            var columnIndex = 0;
            columnIndex < ExpenseExportTable.ColumnHeaders.Count;
            columnIndex++
        )
        {
            var cell = worksheet.Cell(1, columnIndex + 1);
            cell.Value = ExpenseExportTable.ColumnHeaders[columnIndex];
            cell.Style.Font.Bold = true;
        }

        worksheet.SheetView.FreezeRows(1);
    }

    private static void WriteCell(IXLCell cell, ExportCell exportCell)
    {
        if (exportCell.Text is not null)
        {
            cell.Value = exportCell.Text;
        }
        else if (exportCell.Number is not null)
        {
            cell.Value = exportCell.Number.Value;
        }
        else if (exportCell.Date is not null)
        {
            cell.Value = exportCell.Date.Value.ToDateTime(TimeOnly.MinValue);
            cell.Style.DateFormat.Format = DateNumberFormat;
        }
    }
}
