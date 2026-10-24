using System.Globalization;
using Amazon.Textract.Model;
using ProjectDoomsdayServer.Domain.Models.Extraction;
using DomainBoundingBox = ProjectDoomsdayServer.Domain.Models.Extraction.BoundingBox;
using DomainGeometry = ProjectDoomsdayServer.Domain.Models.Extraction.Geometry;
using DomainPoint = ProjectDoomsdayServer.Domain.Models.Extraction.Point;

namespace ProjectDoomsdayServer.Infrastructure.Files;

public static class TextractExpenseMapper
{
    public const string SourceName = "AwsTextract.AnalyzeExpense";

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "M/d/yyyy",
        "M/d/yy",
        "MM/dd/yyyy",
        "MM/dd/yy",
        "M-d-yyyy",
        "M-d-yy",
        "d MMM yyyy",
        "MMM d, yyyy",
        "MMMM d, yyyy",
        "MMM d yyyy",
        "MMM.d'yy",
        "MMM. d, yyyy",
    ];

    public static ExtractedExpenseInfo Map(
        AnalyzeExpenseResponse response,
        DateTimeOffset extractedAtUtc
    )
    {
        var documents = response.ExpenseDocuments ?? [];
        return new ExtractedExpenseInfo
        {
            ExtractedAtUtc = extractedAtUtc,
            Source = SourceName,
            RawText = string.Join("\n", documents.SelectMany(ExtractLineTexts)),
            Expenses = documents.Select(MapExpense).ToList(),
        };
    }

    public static decimal? ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var cleaned = new string(
            text.Where(character => char.IsDigit(character) || character is '.' or '-').ToArray()
        );
        return decimal.TryParse(
            cleaned,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var amount
        )
            ? amount
            : null;
    }

    public static DateOnly? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return DateOnly.TryParseExact(
            text.Trim(),
            DateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date
        )
            ? date
            : null;
    }

    private static IEnumerable<string> ExtractLineTexts(ExpenseDocument document) =>
        (document.Blocks ?? [])
            .Where(block =>
                block.BlockType == Amazon.Textract.BlockType.LINE && block.Text is not null
            )
            .Select(block => block.Text);

    private static Expense MapExpense(ExpenseDocument document)
    {
        var summaryFields = document.SummaryFields ?? [];
        var expense = new Expense
        {
            ExpenseIndex = document.ExpenseIndex ?? 0,
            VendorName = BestOfType(summaryFields, "VENDOR_NAME", MapField),
            VendorAddress = BestOfType(summaryFields, "VENDOR_ADDRESS", MapField),
            VendorPhone = BestOfType(summaryFields, "VENDOR_PHONE", MapField),
            ReceiptId = BestOfType(summaryFields, "INVOICE_RECEIPT_ID", MapField),
            ReceiptDate = BestOfType(summaryFields, "INVOICE_RECEIPT_DATE", MapDateField),
            Subtotal = BestOfType(summaryFields, "SUBTOTAL", MapAmountField),
            Tax = BestOfType(summaryFields, "TAX", MapAmountField),
            Total = BestOfType(summaryFields, "TOTAL", MapAmountField),
            AmountPaid = BestOfType(summaryFields, "AMOUNT_PAID", MapAmountField),
            SummaryFields = summaryFields.Select(MapField).ToList(),
        };

        expense.LineItems = (document.LineItemGroups ?? [])
            .SelectMany(group =>
                (group.LineItems ?? []).Select(item =>
                    MapLineItem(group.LineItemGroupIndex ?? 0, item)
                )
            )
            .ToList();
        return expense;
    }

    private static LineItem MapLineItem(int groupIndex, LineItemFields lineItem)
    {
        var fields = lineItem.LineItemExpenseFields ?? [];
        var mappedTypes = new[]
        {
            "ITEM",
            "QUANTITY",
            "UNIT_PRICE",
            "PRICE",
            "PRODUCT_CODE",
            "EXPENSE_ROW",
        };
        return new LineItem
        {
            GroupIndex = groupIndex,
            Item = BestOfType(fields, "ITEM", MapField),
            Quantity = BestOfType(fields, "QUANTITY", MapAmountField),
            UnitPrice = BestOfType(fields, "UNIT_PRICE", MapAmountField),
            Price = BestOfType(fields, "PRICE", MapAmountField),
            ProductCode = BestOfType(fields, "PRODUCT_CODE", MapField),
            ExpenseRow = BestOfType(fields, "EXPENSE_ROW", MapField),
            OtherFields = fields
                .Where(field => !mappedTypes.Contains(field.Type?.Text))
                .Select(MapField)
                .ToList(),
        };
    }

    private static T? BestOfType<T>(
        IEnumerable<ExpenseField> fields,
        string type,
        Func<ExpenseField, T> map
    )
        where T : class
    {
        var best = fields
            .Where(field => field.Type?.Text == type)
            .OrderByDescending(field => field.ValueDetection?.Confidence ?? 0)
            .FirstOrDefault();
        return best is null ? null : map(best);
    }

    private static ExtractedField MapField(ExpenseField field) =>
        ApplyCommon(new ExtractedField(), field);

    private static ExtractedAmountField MapAmountField(ExpenseField field)
    {
        var mapped = ApplyCommon(new ExtractedAmountField(), field);
        mapped.Amount = ParseAmount(mapped.Text);
        return mapped;
    }

    private static ExtractedDateField MapDateField(ExpenseField field)
    {
        var mapped = ApplyCommon(new ExtractedDateField(), field);
        mapped.Date = ParseDate(mapped.Text);
        return mapped;
    }

    private static T ApplyCommon<T>(T target, ExpenseField field)
        where T : ExtractedField
    {
        target.Type = field.Type?.Text ?? string.Empty;
        target.Label = field.LabelDetection?.Text;
        target.LabelConfidence = field.LabelDetection?.Confidence;
        target.Text = field.ValueDetection?.Text ?? string.Empty;
        target.Confidence = field.ValueDetection?.Confidence ?? 0;
        target.Geometry = MapGeometry(field.ValueDetection?.Geometry, field.PageNumber ?? 1);
        return target;
    }

    private static DomainGeometry? MapGeometry(
        Amazon.Textract.Model.Geometry? geometry,
        int pageNumber
    )
    {
        if (geometry?.BoundingBox is null)
            return null;
        return new DomainGeometry
        {
            PageNumber = pageNumber,
            BoundingBox = new DomainBoundingBox
            {
                Left = geometry.BoundingBox.Left ?? 0,
                Top = geometry.BoundingBox.Top ?? 0,
                Width = geometry.BoundingBox.Width ?? 0,
                Height = geometry.BoundingBox.Height ?? 0,
            },
            Polygon = (geometry.Polygon ?? [])
                .Select(point => new DomainPoint { X = point.X ?? 0, Y = point.Y ?? 0 })
                .ToList(),
        };
    }
}
