using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.Domain.Models.TextExtraction;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileCsvExportTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Header =
        "Vendor.Name,Vendor.Address,Vendor.Phone,ReceiptId,ReceiptDate,Subtotal,Tax,Total,AmountPaid,"
        + "LineItem.Item,LineItem.Quantity,LineItem.Price,LineItem.UnitPrice,LineItem.ProductCode,LineItem.ExpenseRow";

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
            "/files",
            new CreateFileInput
            {
                FileName = "receipt.jpg",
                ContentType = "image/jpeg",
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
            Text = amount.ToString(),
            Amount = amount,
        };

    [Fact]
    public async Task ExportToCsv_WithLineItems_ReturnsOneRowPerLineItem()
    {
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

        var response = await _client.GetAsync($"/files/{fileId}/export/csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var body = await response.Content.ReadAsStringAsync();
        var vendorCells =
            "\"Joe's \"\"Diner\"\", Inc\",\"1 Main St\nSpringfield\",N/A,N/A,2024-03-05,N/A,N/A,11.5,N/A,";
        body.Should()
            .Be(
                Header
                    + "\r\n"
                    + vendorCells
                    + "Burger,2,8.25,N/A,N/A,N/A\r\n"
                    + vendorCells
                    + "Fries,N/A,N/A,N/A,N/A,N/A\r\n"
            );
    }

    [Fact]
    public async Task ExportToCsv_ExpenseWithoutLineItems_ReturnsSingleRow()
    {
        var fileId = await CreateFileAsync(
            new ExtractionInfo { Expenses = [new Expense { VendorName = Field("Shop") }] }
        );

        var response = await _client.GetAsync($"/files/{fileId}/export/csv");

        var body = await response.Content.ReadAsStringAsync();
        body.Should()
            .Be(Header + "\r\n" + "Shop" + string.Concat(Enumerable.Repeat(",N/A", 14)) + "\r\n");
    }

    [Fact]
    public async Task ExportToCsv_WithoutExtraction_ReturnsHeaderOnly()
    {
        var fileId = await CreateFileAsync(null);

        var response = await _client.GetAsync($"/files/{fileId}/export/csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(Header + "\r\n");
    }

    [Fact]
    public async Task ExportToCsv_UnknownFile_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/files/does-not-exist/export/csv");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
