using ProjectDoomsdayServer.Domain.Models.TextExtraction;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.Application.Files.Services;

/// <summary>
/// Format-agnostic tabular view of a file's extracted expenses, shared by all exporters.
/// </summary>
public static class ExpenseExportTable
{
    public const string TextFallback = "N/A";

    public static IReadOnlyList<string> ColumnHeaders { get; } =
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

    /// <summary>
    /// One row per line item, one row for an expense without line items, none without extraction.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<ExportCell>> BuildRows(File file)
    {
        var rows = new List<IReadOnlyList<ExportCell>>();
        foreach (var expense in file.ExtractionInfo?.Expenses ?? [])
        {
            ExportCell[] expenseCells =
            [
                Text(expense.VendorName),
                Text(expense.VendorAddress),
                Text(expense.VendorPhone),
                Text(expense.ReceiptId),
                Date(expense.ReceiptDate),
                Number(expense.Subtotal),
                Number(expense.Tax),
                Number(expense.Total),
                Number(expense.AmountPaid),
            ];

            if (expense.LineItems.Count == 0)
            {
                rows.Add([.. expenseCells, .. LineItemCells(null)]);
                continue;
            }

            foreach (var lineItem in expense.LineItems)
            {
                rows.Add([.. expenseCells, .. LineItemCells(lineItem)]);
            }
        }

        return rows;
    }

    private static ExportCell[] LineItemCells(LineItem? lineItem) =>
        [
            Text(lineItem?.Item),
            Number(lineItem?.Quantity),
            Number(lineItem?.Price),
            Number(lineItem?.UnitPrice),
            Text(lineItem?.ProductCode),
            Text(lineItem?.ExpenseRow),
        ];

    private static ExportCell Text(ExtractedField? field) =>
        ExportCell.FromText(field?.Text ?? TextFallback);

    private static ExportCell Number(AmountField? field) => ExportCell.FromNumber(field?.Amount);

    private static ExportCell Date(DateField? field) => ExportCell.FromDate(field?.Date);
}
