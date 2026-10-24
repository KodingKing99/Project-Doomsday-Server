using Amazon.Textract;
using Amazon.Textract.Model;

namespace ProjectDoomsdayServer.UnitTests;

/// <summary>Hand-built Textract response modelled on the McDonald's receipt capture.</summary>
public static class McDonaldsReceiptResponse
{
    public static AnalyzeExpenseResponse Create() =>
        new() { ExpenseDocuments = [CreateDocument(1)] };

    public static ExpenseDocument CreateDocument(int expenseIndex) =>
        new()
        {
            ExpenseIndex = expenseIndex,
            Blocks =
            [
                Line("McDonald's Corporation"),
                Line("Thank you for eating at McDonald's"),
                new Block { BlockType = BlockType.WORD, Text = "ignored" },
            ],
            SummaryFields = CreateSummaryFields(),
            LineItemGroups =
            [
                new LineItemGroup
                {
                    LineItemGroupIndex = 1,
                    LineItems =
                    [
                        LineItem("BIG BREAKFAST", "1", "2.99"),
                        LineItem("HOTCAKES", "1", "2.29"),
                        LineItem("2 EGG MCMUFFIN", "1", "3.00"),
                        LineItem("SAU EGG CH MCGRIDDLE", "1", "2.49"),
                    ],
                },
            ],
        };

    private static List<ExpenseField> CreateSummaryFields()
    {
        var fields = new List<ExpenseField>
        {
            Field("VENDOR_NAME", "McDonald's", 99.99f),
            Field("VENDOR_NAME", "McDonald's Corporation", 99.97f),
            Field("VENDOR_PHONE", "(856)327-0606", 99f, "TEL#"),
            Field("INVOICE_RECEIPT_ID", "KB34463365001", 99f, "MER#"),
            Field("INVOICE_RECEIPT_DATE", "Jun.22'08(Sun)08:", 79.14f),
            Field("SUBTOTAL", "10.77", 99.99f, "SUB TOTAL"),
            Field("TAX", "0.75", 99.9f, "TAKE OUT TAX"),
            Field("TOTAL", "11.52", 99.99f),
            Field("AMOUNT_PAID", "11.52", 99.9f, "TRANSACTION AMOUNT"),
            Field("OTHER", "6038", 99f, "STORE#"),
        };
        return fields;
    }

    private static Block Line(string text) => new() { BlockType = BlockType.LINE, Text = text };

    private static LineItemFields LineItem(string item, string quantity, string price) =>
        new()
        {
            LineItemExpenseFields =
            [
                Field("ITEM", item, 99.99f),
                Field("QUANTITY", quantity, 99f),
                Field("PRICE", price, 99f),
                Field("EXPENSE_ROW", $"{quantity} {item} {price}", 99f),
            ],
        };

    public static ExpenseField Field(
        string type,
        string text,
        float confidence,
        string? label = null
    ) =>
        new()
        {
            Type = new ExpenseType { Text = type, Confidence = 99f },
            LabelDetection = label is null
                ? null
                : new ExpenseDetection { Text = label, Confidence = 98f },
            ValueDetection = new ExpenseDetection
            {
                Text = text,
                Confidence = confidence,
                Geometry = new Geometry
                {
                    BoundingBox = new BoundingBox
                    {
                        Left = 0.5f,
                        Top = 0.25f,
                        Width = 0.125f,
                        Height = 0.0625f,
                    },
                    Polygon =
                    [
                        new Point { X = 0.5f, Y = 0.25f },
                        new Point { X = 0.625f, Y = 0.25f },
                        new Point { X = 0.625f, Y = 0.3125f },
                        new Point { X = 0.5f, Y = 0.3125f },
                    ],
                },
            },
            PageNumber = 1,
        };
}
