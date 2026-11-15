using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.Domain.Models.TextExtraction;
using ProjectDoomsdayServer.TestSupport;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileExcelExportTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string NonExistentFileId = "does-not-exist";
    private const string VendorNameColumnHeader = "Vendor.Name";
    private const string TextFallback = "N/A";
    private const string ShopName = "Shop";
    private const string BurgerName = "Burger";
    private const string FriesName = "Fries";
    private const string DinerName = "Joe's \"Diner\", Inc";

    private const int ColumnCount = 15;
    private const int VendorNameColumn = 1;
    private const int ReceiptDateColumn = 5;
    private const int TotalColumn = 8;
    private const int LineItemItemColumn = 10;
    private const int LineItemQuantityColumn = 11;
    private const int LineItemPriceColumn = 12;
    private const int HeaderRow = 1;
    private const int FirstWorksheetPosition = 1;
    private const int FirstDataRow = 2;
    private const int SecondDataRow = 3;
    private const int UploadSizeBytes = 10;

    private const decimal TotalAmount = 11.5m;
    private const decimal BurgerQuantity = 2m;
    private const decimal BurgerPrice = 8.25m;

    private static readonly DateOnly _receiptDate = new(2024, 3, 5);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileExcelExportTests(CustomWebApplicationFactory factory)
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
                SizeBytes = UploadSizeBytes,
            }
        );
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = result!.File.Id!;
        _factory.FileRecords[fileId].ExtractionInfo = extractionInfo;
        return fileId;
    }

    private static async Task<IXLWorksheet> ReadWorksheetAsync(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var workbook = new XLWorkbook(new MemoryStream(bytes));
        return workbook.Worksheet(FirstWorksheetPosition);
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
    public async Task ExportToExcel_WithLineItems_ReturnsOneRowPerLineItem()
    {
        // Arrange
        var fileId = await CreateFileAsync(
            new ExtractionInfo
            {
                Expenses =
                [
                    new Expense
                    {
                        VendorName = Field(DinerName),
                        ReceiptDate = new DateField { Type = "D", Date = _receiptDate },
                        Total = Amount(TotalAmount),
                        LineItems =
                        [
                            new LineItem
                            {
                                Item = Field(BurgerName),
                                Quantity = Amount(BurgerQuantity),
                                Price = Amount(BurgerPrice),
                            },
                            new LineItem { Item = Field(FriesName) },
                        ],
                    },
                ],
            }
        );

        // Act
        var response = await _client.GetAsync(FileRoutes.ExportExcel(fileId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ExcelContentType);
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be($"{fileId}.xlsx");
        var worksheet = await ReadWorksheetAsync(response);
        worksheet.LastRowUsed()!.RowNumber().Should().Be(SecondDataRow);
        worksheet.Cell(HeaderRow, VendorNameColumn).GetString().Should().Be(VendorNameColumnHeader);
        worksheet.Cell(FirstDataRow, VendorNameColumn).GetString().Should().Be(DinerName);
        worksheet.Cell(SecondDataRow, VendorNameColumn).GetString().Should().Be(DinerName);
        worksheet.Cell(FirstDataRow, ReceiptDateColumn).DataType.Should().Be(XLDataType.DateTime);
        DateOnly
            .FromDateTime(worksheet.Cell(FirstDataRow, ReceiptDateColumn).GetDateTime())
            .Should()
            .Be(_receiptDate);
        worksheet.Cell(FirstDataRow, TotalColumn).DataType.Should().Be(XLDataType.Number);
        worksheet.Cell(FirstDataRow, TotalColumn).GetDouble().Should().Be((double)TotalAmount);
        worksheet.Cell(FirstDataRow, LineItemItemColumn).GetString().Should().Be(BurgerName);
        worksheet
            .Cell(FirstDataRow, LineItemQuantityColumn)
            .DataType.Should()
            .Be(XLDataType.Number);
        worksheet
            .Cell(FirstDataRow, LineItemQuantityColumn)
            .GetDouble()
            .Should()
            .Be((double)BurgerQuantity);
        worksheet
            .Cell(FirstDataRow, LineItemPriceColumn)
            .GetDouble()
            .Should()
            .Be((double)BurgerPrice);
        worksheet.Cell(SecondDataRow, LineItemItemColumn).GetString().Should().Be(FriesName);
        worksheet.Cell(SecondDataRow, LineItemQuantityColumn).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public async Task ExportToExcel_ExpenseWithoutLineItems_ReturnsSingleRow()
    {
        // Arrange
        var fileId = await CreateFileAsync(
            new ExtractionInfo { Expenses = [new Expense { VendorName = Field(ShopName) }] }
        );

        // Act
        var response = await _client.GetAsync(FileRoutes.ExportExcel(fileId));

        // Assert
        var worksheet = await ReadWorksheetAsync(response);
        worksheet.LastRowUsed()!.RowNumber().Should().Be(FirstDataRow);
        worksheet.Cell(FirstDataRow, VendorNameColumn).GetString().Should().Be(ShopName);
        worksheet.Cell(FirstDataRow, LineItemItemColumn).GetString().Should().Be(TextFallback);
    }

    [Fact]
    public async Task ExportToExcel_MissingReceiptDate_LeavesDateCellEmpty()
    {
        // Arrange
        var fileId = await CreateFileAsync(
            new ExtractionInfo { Expenses = [new Expense { VendorName = Field(ShopName) }] }
        );

        // Act
        var response = await _client.GetAsync(FileRoutes.ExportExcel(fileId));

        // Assert
        var worksheet = await ReadWorksheetAsync(response);
        worksheet.Cell(FirstDataRow, ReceiptDateColumn).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public async Task ExportToExcel_WithoutExtraction_ReturnsHeaderOnly()
    {
        // Arrange
        var fileId = await CreateFileAsync(null);

        // Act
        var response = await _client.GetAsync(FileRoutes.ExportExcel(fileId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var worksheet = await ReadWorksheetAsync(response);
        worksheet.LastRowUsed()!.RowNumber().Should().Be(HeaderRow);
        worksheet.LastColumnUsed()!.ColumnNumber().Should().Be(ColumnCount);
    }

    [Fact]
    public async Task ExportToExcel_UnknownFile_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync(FileRoutes.ExportExcel(NonExistentFileId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
