using System.Globalization;
using System.Text;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Application.Interfaces;

namespace ProjectDoomsdayServer.Application.Files.Services;

public sealed class CsvExportService : IFileExportService
{
    private const string CalendarDateFormat = "yyyy-MM-dd";
    private const string CsvContentType = "text/csv";

    private readonly IFileService _fileService;

    public CsvExportService(IFileService fileService)
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

        var builder = new StringBuilder();
        AppendRow(builder, ExpenseExportTable.ColumnHeaders);
        foreach (var row in ExpenseExportTable.BuildRows(file))
        {
            AppendRow(builder, row.Select(FormatCell));
        }

        return new ExportFileResult(
            Encoding.UTF8.GetBytes(builder.ToString()),
            CsvContentType,
            $"{request.FileId}.csv"
        );
    }

    private static string FormatCell(ExportCell cell) =>
        cell.Text
        ?? cell.Number?.ToString(CultureInfo.InvariantCulture)
        ?? cell.Date?.ToString(CalendarDateFormat, CultureInfo.InvariantCulture)
        ?? string.Empty;

    private static void AppendRow(StringBuilder builder, IEnumerable<string> cells)
    {
        builder.AppendJoin(',', cells.Select(Escape)).Append("\r\n");
    }

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : "\"" + value.Replace("\"", "\"\"") + "\"";
}
