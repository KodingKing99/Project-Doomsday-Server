using Amazon.Textract.Model;
using FluentAssertions;
using ProjectDoomsdayServer.Infrastructure.Files;

namespace ProjectDoomsdayServer.UnitTests;

public class TextractExpenseMapperTests
{
    private const string OtherFieldText = "x";

    private static readonly DateTimeOffset _extractedAt = new(2026, 9, 23, 17, 0, 0, TimeSpan.Zero);

    private static Domain.Models.TextExtraction.Expense MapSingleExpense() =>
        TextractExpenseMapper
            .Map(McDonaldsReceiptResponse.Create(), _extractedAt)
            .Expenses.Single();

    [Fact]
    public void Map_SetsSourceAndTimestamp()
    {
        // Arrange
        var response = McDonaldsReceiptResponse.Create();

        // Act
        var result = TextractExpenseMapper.Map(response, _extractedAt);

        // Assert
        result.Source.Should().Be("AwsTextract.AnalyzeExpense");
        result.ExtractedAtUtc.Should().Be(_extractedAt);
    }

    [Fact]
    public void Map_MapsAmountsWithRawText()
    {
        // Act
        var expense = MapSingleExpense();

        // Assert
        expense.Total!.Amount.Should().Be(11.52m);
        expense.Total.Text.Should().Be("11.52");
        expense.Subtotal!.Amount.Should().Be(10.77m);
        expense.Tax!.Amount.Should().Be(0.75m);
        expense.AmountPaid!.Amount.Should().Be(11.52m);
    }

    [Fact]
    public void Map_VendorNameTakesHighestConfidence_AndKeepsAllSummaryFields()
    {
        // Act
        var expense = MapSingleExpense();

        // Assert
        expense.VendorName!.Text.Should().Be("McDonald's");
        expense
            .SummaryFields.Count(field =>
                string.Equals(field.Type, "VENDOR_NAME", StringComparison.Ordinal)
            )
            .Should()
            .Be(2);
        expense.SummaryFields.Should().HaveCount(10);
    }

    [Fact]
    public void Map_FlattensLineItems()
    {
        // Act
        var lineItems = MapSingleExpense().LineItems;

        // Assert
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
        // Arrange
        var response = McDonaldsReceiptResponse.Create();
        response
            .ExpenseDocuments[0]
            .LineItemGroups[0]
            .LineItems[0]
            .LineItemExpenseFields.Add(
                McDonaldsReceiptResponse.Field("OTHER", OtherFieldText, 90f)
            );

        // Act
        var lineItem = TextractExpenseMapper.Map(response, _extractedAt).Expenses[0].LineItems[0];

        // Assert
        lineItem.OtherFields.Should().ContainSingle().Which.Text.Should().Be(OtherFieldText);
    }

    [Fact]
    public void Map_GeometryIsNormalizedWithFourPointPolygon()
    {
        // Act
        var geometry = MapSingleExpense().Total!.Geometry!;

        // Assert
        geometry.PageNumber.Should().Be(1);
        geometry.BoundingBox.Left.Should().BeInRange(0, 1);
        geometry.BoundingBox.Width.Should().BeApproximately(0.125, 1e-6);
        geometry.Polygon.Should().HaveCount(4);
    }

    [Fact]
    public void Map_LabelsHaveNoGeometry()
    {
        // Act
        var total = MapSingleExpense().Subtotal!;

        // Assert
        total.Label.Should().Be("SUB TOTAL");
        total.LabelConfidence.Should().NotBeNull();
    }

    [Fact]
    public void Map_UnparseableReceiptDate_KeepsTextWithNullDate()
    {
        // Act
        var receiptDate = MapSingleExpense().ReceiptDate!;

        // Assert
        receiptDate.Date.Should().BeNull();
        receiptDate.Text.Should().Be("Jun.22'08(Sun)08:");
    }

    [Fact]
    public void Map_RawTextContainsOnlyLineBlocks()
    {
        // Arrange
        var response = McDonaldsReceiptResponse.Create();

        // Act
        var result = TextractExpenseMapper.Map(response, _extractedAt);

        // Assert
        result.RawText.Should().Be("McDonald's Corporation\nThank you for eating at McDonald's");
    }

    [Fact]
    public void Map_EmptyResponse_HasNoExpenses()
    {
        // Arrange
        var response = new AnalyzeExpenseResponse();

        // Act
        var result = TextractExpenseMapper.Map(response, _extractedAt);

        // Assert
        result.Expenses.Should().BeEmpty();
        result.RawText.Should().BeEmpty();
    }

    [Fact]
    public void Map_TwoDocuments_ProducesTwoExpenses()
    {
        // Arrange
        var response = new AnalyzeExpenseResponse
        {
            ExpenseDocuments =
            [
                McDonaldsReceiptResponse.CreateDocument(1),
                McDonaldsReceiptResponse.CreateDocument(2),
            ],
        };

        // Act
        var result = TextractExpenseMapper.Map(response, _extractedAt);

        // Assert
        result.Expenses.Select(expense => expense.ExpenseIndex).Should().Equal(1, 2);
    }

    [Theory]
    [InlineData("$1,234.50", 1234.50)]
    [InlineData("11.52", 11.52)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseAmount_HandlesEdgeCases(string? text, double? expected)
    {
        // Act
        var result = TextractExpenseMapper.ParseAmount(text);

        // Assert
        result.Should().Be(expected is null ? null : (decimal)expected);
    }

    [Theory]
    [InlineData("2008-06-22", 2008, 6, 22)]
    [InlineData("6/22/2008", 2008, 6, 22)]
    public void ParseDate_ParsesCommonFormats(string text, int year, int month, int day)
    {
        // Act
        var result = TextractExpenseMapper.ParseDate(text);

        // Assert
        result.Should().Be(new DateOnly(year, month, day));
    }
}
