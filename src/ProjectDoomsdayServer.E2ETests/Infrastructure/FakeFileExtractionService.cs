using ProjectDoomsdayServer.Application.Interfaces;
using ProjectDoomsdayServer.Domain.Models.Extraction;

namespace ProjectDoomsdayServer.E2ETests.Infrastructure;

public sealed class FakeFileExtractionService : IFileExtractionService
{
    private int _callCount;

    public int CallCount => _callCount;

    public Task<ExtractedExpenseInfo> ExtractExpenseInformationAsync(
        MemoryStream stream,
        CancellationToken cancellationToken
    )
    {
        Interlocked.Increment(ref _callCount);
        return Task.FromResult(CreateSample());
    }

    public static ExtractedExpenseInfo CreateSample() =>
        new()
        {
            ExtractedAtUtc = new DateTimeOffset(2026, 9, 23, 17, 4, 11, TimeSpan.Zero),
            Source = "Fake",
            RawText = "McDonald's Corporation\n11.52",
            Expenses =
            [
                new Expense
                {
                    ExpenseIndex = 1,
                    VendorName = new ExtractedField
                    {
                        Type = "VENDOR_NAME",
                        Text = "McDonald's",
                        Confidence = 99.99,
                    },
                    ReceiptDate = new ExtractedDateField
                    {
                        Type = "INVOICE_RECEIPT_DATE",
                        Text = "2008-06-22",
                        Date = new DateOnly(2008, 6, 22),
                    },
                    Total = new ExtractedAmountField
                    {
                        Type = "TOTAL",
                        Text = "11.52",
                        Amount = 11.52m,
                        Confidence = 99.99,
                        Geometry = new Geometry
                        {
                            PageNumber = 1,
                            BoundingBox = new BoundingBox
                            {
                                Left = 0.6766,
                                Top = 0.7576,
                                Width = 0.061,
                                Height = 0.0194,
                            },
                            Polygon =
                            [
                                new Point { X = 0.6774, Y = 0.7576 },
                                new Point { X = 0.7376, Y = 0.7576 },
                                new Point { X = 0.7376, Y = 0.777 },
                                new Point { X = 0.6774, Y = 0.777 },
                            ],
                        },
                    },
                    LineItems =
                    [
                        new LineItem
                        {
                            GroupIndex = 1,
                            Item = new ExtractedField { Type = "ITEM", Text = "HOTCAKES" },
                            Price = new ExtractedAmountField
                            {
                                Type = "PRICE",
                                Text = "2.29",
                                Amount = 2.29m,
                            },
                        },
                    ],
                },
            ],
        };
}
