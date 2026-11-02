using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.Domain.Models.TextExtraction;
using ProjectDoomsdayServer.TestSupport;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileCsvExportTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Header =
        "Vendor.Name,Vendor.Address,Vendor.Phone,ReceiptId,ReceiptDate,Subtotal,Tax,Total,AmountPaid,"
        + "LineItem.Item,LineItem.Quantity,LineItem.Price,LineItem.UnitPrice,LineItem.ProductCode,LineItem.ExpenseRow";

    private const string CsvLineBreak = "\r\n";

    private const string NonExistentFileId = "does-not-exist";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileCsvExportTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _client = factory.CreateClient();
    }

    private async Task<string> CreateFileAsync(ExtractionInfo? extractionInfo)
    {
        var response = await _client.PostAsJsonAsync(
            FileRoutes.Base,
            new CreateFileInput
            {
                FileName = TestConstants.ReceiptFileName,
                ContentType = TestConstants.ImageJpegContentType,
                SizeBytes = 10,
            }
        );
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = result!.File.Id!;
        _factory.FileRecords[fileId].ExtractionInfo = extractionInfo;
        return fileId;
    }

    private static ExtractedField Field(string text) => new() { Type = "T", Text = text };

    private static AmountField Amount(decimal amount) =>
        new()
        {
            Type = "A",
            Text = amount.ToString(CultureInfo.InvariantCulture),
            Amount = amount,
        };

    [Fact]
    public async Task ExportToCsv_WithLineItems_ReturnsOneRowPerLineItem()
    {
        // Arrange
        var fileId = await CreateFileAsync(
            new ExtractionInfo
            {
                Expenses =
                [
                    new Expense
                    {
                        VendorName = Field("Joe's \"Diner\", Inc"),
                        VendorAddress = Field("1 Main St\nSpringfield"),
                        ReceiptDate = new DateField { Type = "D", Date = new DateOnly(2024, 3, 5) },
                        Total = Amount(11.5m),
                        LineItems =
                        [
                            new LineItem
                            {
                                Item = Field("Burger"),
                                Quantity = Amount(2),
                                Price = Amount(8.25m),
                            },
                            new LineItem { Item = Field("Fries") },
                        ],
                    },
                ],
            }
        );

        // Act
        var response = await _client.GetAsync(FileRoutes.ExportCsv(fileId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var body = await response.Content.ReadAsStringAsync();
        var vendorCells =
            "\"Joe's \"\"Diner\"\", Inc\",\"1 Main St\nSpringfield\",N/A,N/A,2024-03-05,,,11.5,,";
        body.Should()
            .Be(
                Header
                    + CsvLineBreak
                    + vendorCells
                    + "Burger,2,8.25,,N/A,N/A"
                    + CsvLineBreak
                    + vendorCells
                    + "Fries,,,,N/A,N/A"
                    + CsvLineBreak
            );
    }

    [Fact]
    public async Task ExportToCsv_ExpenseWithoutLineItems_ReturnsSingleRow()
    {
        // Arrange
        var fileId = await CreateFileAsync(
            new ExtractionInfo { Expenses = [new Expense { VendorName = Field("Shop") }] }
        );

        // Act
        var response = await _client.GetAsync(FileRoutes.ExportCsv(fileId));

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        body.Should()
            .Be(Header + CsvLineBreak + "Shop,N/A,N/A,N/A,,,,,,N/A,,,,N/A,N/A" + CsvLineBreak);
    }

    [Fact]
    public async Task ExportToCsv_WithoutExtraction_ReturnsHeaderOnly()
    {
        // Arrange
        var fileId = await CreateFileAsync(null);

        // Act
        var response = await _client.GetAsync(FileRoutes.ExportCsv(fileId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(Header + CsvLineBreak);
    }

    [Fact]
    public async Task ExportToCsv_UnknownFile_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync(FileRoutes.ExportCsv(NonExistentFileId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
