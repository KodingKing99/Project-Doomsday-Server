using Amazon.Textract.Model;
using FluentAssertions;
using ProjectDoomsdayServer.Infrastructure.Files;

namespace ProjectDoomsdayServer.UnitTests;

public class TextractExpenseMapperTests
{
    private static readonly DateTimeOffset ExtractedAt = new(2026, 9, 23, 17, 0, 0, TimeSpan.Zero);

    private static Domain.Models.Extraction.Expense MapSingleExpense() =>
        TextractExpenseMapper.Map(McDonaldsReceiptResponse.Create(), ExtractedAt).Expenses.Single();

    [Fact]
    public void Map_SetsSourceAndTimestamp()
    {
        var result = TextractExpenseMapper.Map(McDonaldsReceiptResponse.Create(), ExtractedAt);

        result.Source.Should().Be("AwsTextract.AnalyzeExpense");
        result.ExtractedAtUtc.Should().Be(ExtractedAt);
    }

    [Fact]
    public void Map_MapsAmountsWithRawText()
    {
        var expense = MapSingleExpense();

        expense.Total!.Amount.Should().Be(11.52m);
        expense.Total.Text.Should().Be("11.52");
        expense.Subtotal!.Amount.Should().Be(10.77m);
        expense.Tax!.Amount.Should().Be(0.75m);
        expense.AmountPaid!.Amount.Should().Be(11.52m);
    }

    [Fact]
    public void Map_VendorNameTakesHighestConfidence_AndKeepsAllSummaryFields()
    {
        var expense = MapSingleExpense();

        expense.VendorName!.Text.Should().Be("McDonald's");
        expense.SummaryFields.Count(field => field.Type == "VENDOR_NAME").Should().Be(2);
        expense.SummaryFields.Should().HaveCount(10);
    }

    [Fact]
    public void Map_FlattensLineItems()
    {
        var lineItems = MapSingleExpense().LineItems;

        lineItems.Should().HaveCount(4);
        lineItems[0].Item!.Text.Should().Be("BIG BREAKFAST");
        lineItems[0].Price!.Amount.Should().Be(2.99m);
        lineItems[0].Quantity!.Amount.Should().Be(1m);
        lineItems[0].UnitPrice.Should().BeNull();
        lineItems[0].GroupIndex.Should().Be(1);
        lineItems[0].ExpenseRow!.Geometry.Should().NotBeNull();
    }

    [Fact]
    public void Map_UnmappedLineItemFieldsGoToOtherFields()
    {
        var response = McDonaldsReceiptResponse.Create();
        response
            .ExpenseDocuments[0]
            .LineItemGroups[0]
            .LineItems[0]
            .LineItemExpenseFields.Add(McDonaldsReceiptResponse.Field("OTHER", "x", 90f));

        var lineItem = TextractExpenseMapper.Map(response, ExtractedAt).Expenses[0].LineItems[0];

        lineItem.OtherFields.Should().ContainSingle().Which.Text.Should().Be("x");
    }

    [Fact]
    public void Map_GeometryIsNormalizedWithFourPointPolygon()
    {
        var geometry = MapSingleExpense().Total!.Geometry!;

        geometry.PageNumber.Should().Be(1);
        geometry.BoundingBox.Left.Should().BeInRange(0, 1);
        geometry.BoundingBox.Width.Should().BeApproximately(0.125, 1e-6);
        geometry.Polygon.Should().HaveCount(4);
    }

    [Fact]
    public void Map_LabelsHaveNoGeometry()
    {
        var total = MapSingleExpense().Subtotal!;

        total.Label.Should().Be("SUB TOTAL");
        total.LabelConfidence.Should().NotBeNull();
    }

    [Fact]
    public void Map_UnparseableReceiptDate_KeepsTextWithNullDate()
    {
        var receiptDate = MapSingleExpense().ReceiptDate!;

        receiptDate.Date.Should().BeNull();
        receiptDate.Text.Should().Be("Jun.22'08(Sun)08:");
    }

    [Fact]
    public void Map_RawTextContainsOnlyLineBlocks()
    {
        var result = TextractExpenseMapper.Map(McDonaldsReceiptResponse.Create(), ExtractedAt);

        result.RawText.Should().Be("McDonald's Corporation\nThank you for eating at McDonald's");
    }

    [Fact]
    public void Map_EmptyResponse_HasNoExpenses()
    {
        var result = TextractExpenseMapper.Map(new AnalyzeExpenseResponse(), ExtractedAt);

        result.Expenses.Should().BeEmpty();
        result.RawText.Should().BeEmpty();
    }

    [Fact]
    public void Map_TwoDocuments_ProducesTwoExpenses()
    {
        var response = new AnalyzeExpenseResponse
        {
            ExpenseDocuments =
            [
                McDonaldsReceiptResponse.CreateDocument(1),
                McDonaldsReceiptResponse.CreateDocument(2),
            ],
        };

        var result = TextractExpenseMapper.Map(response, ExtractedAt);

        result.Expenses.Select(expense => expense.ExpenseIndex).Should().Equal(1, 2);
    }

    [Theory]
    [InlineData("$1,234.50", 1234.50)]
    [InlineData("11.52", 11.52)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseAmount_HandlesEdgeCases(string? text, double? expected) =>
        TextractExpenseMapper
            .ParseAmount(text)
            .Should()
            .Be(expected is null ? null : (decimal)expected);

    [Theory]
    [InlineData("2008-06-22", 2008, 6, 22)]
    [InlineData("6/22/2008", 2008, 6, 22)]
    public void ParseDate_ParsesCommonFormats(string text, int year, int month, int day) =>
        TextractExpenseMapper.ParseDate(text).Should().Be(new DateOnly(year, month, day));
}
