using System.Globalization;
using System.Text;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Interfaces;
using ProjectDoomsdayServer.Domain.Models.TextExtraction;

namespace ProjectDoomsdayServer.Application.Files.Services;

public sealed class CsvExportService : ICsvExportService
{
    private const string TextFallback = "N/A";
    private const string CalendarDateFormat = "yyyy-MM-dd";

    private static readonly string[] Columns =
    [
        "Vendor.Name",
        "Vendor.Address",
        "Vendor.Phone",
        "ReceiptId",
        "ReceiptDate",
        "Subtotal",
        "Tax",
        "Total",
        "AmountPaid",
        "LineItem.Item",
        "LineItem.Quantity",
        "LineItem.Price",
        "LineItem.UnitPrice",
        "LineItem.ProductCode",
        "LineItem.ExpenseRow",
    ];

    private readonly IFileService _fileService;

    public CsvExportService(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<string> ToCsv(
        ExportFileToCsvRequest request,
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

        var csv = new StringBuilder();
        AppendRow(csv, Columns);
        // Expense csv for receipts
        foreach (var expense in file.ExtractionInfo?.Expenses ?? [])
        {
            var expenseCells = new[]
            {
                CreateTextCsvField(expense.VendorName),
                CreateTextCsvField(expense.VendorAddress),
                CreateTextCsvField(expense.VendorPhone),
                CreateTextCsvField(expense.ReceiptId),
                CreateDateCsvField(expense.ReceiptDate),
                CreateAmmountCsvField(expense.Subtotal),
                CreateAmmountCsvField(expense.Tax),
                CreateAmmountCsvField(expense.Total),
                CreateAmmountCsvField(expense.AmountPaid),
            };

            if (expense.LineItems.Count == 0)
            {
                AppendRow(csv, [.. expenseCells, .. LineItemCells(null)]);
                continue;
            }

            foreach (var lineItem in expense.LineItems)
            {
                AppendRow(csv, [.. expenseCells, .. LineItemCells(lineItem)]);
            }
        }

        return csv.ToString();
    }

    private static string[] LineItemCells(LineItem? lineItem) =>
        [
            CreateTextCsvField(lineItem?.Item),
            CreateAmmountCsvField(lineItem?.Quantity),
            CreateAmmountCsvField(lineItem?.Price),
            CreateAmmountCsvField(lineItem?.UnitPrice),
            CreateTextCsvField(lineItem?.ProductCode),
            CreateTextCsvField(lineItem?.ExpenseRow),
        ];

    private static string CreateTextCsvField(ExtractedField? field) => field?.Text ?? TextFallback;

    private static string CreateAmmountCsvField(AmountField? field) =>
        field?.Amount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string CreateDateCsvField(DateField? field) =>
        field?.Date?.ToString(CalendarDateFormat, CultureInfo.InvariantCulture) ?? string.Empty;

    private static void AppendRow(StringBuilder csv, IEnumerable<string> cells)
    {
        csv.Append(string.Join(',', cells.Select(Escape))).Append("\r\n");
    }

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : "\"" + value.Replace("\"", "\"\"") + "\"";
}
