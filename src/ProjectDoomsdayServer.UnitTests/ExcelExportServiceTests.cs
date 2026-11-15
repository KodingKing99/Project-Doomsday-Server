using ClosedXML.Excel;
using FluentAssertions;
using NSubstitute;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Services;
using ProjectDoomsdayServer.Application.Interfaces;
using ProjectDoomsdayServer.Domain.Models.TextExtraction;
using ProjectDoomsdayServer.Infrastructure.Files;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.UnitTests;

public class ExcelExportServiceTests
{
    private const string FileId = "file-1";
    private const string UserId = "user-1";
    private const string VendorName = "Burger Place";
    private const string ItemName = "Fries";
    private const string TextFallback = "N/A";
    private const int VendorNameColumn = 1;
    private const int ReceiptDateColumn = 5;
    private const int TotalColumn = 8;
    private const int AmountPaidColumn = 9;
    private const int LineItemItemColumn = 10;
    private const int LineItemPriceColumn = 12;
    private const decimal ExpenseTotal = 12.50m;
    private const decimal LineItemPrice = 3.25m;
    private const int HeaderRow = 1;
    private const int FirstDataRow = 2;

    private static readonly DateOnly _receiptDate = new(2026, 3, 15);

    private readonly IFileService _fileService = Substitute.For<IFileService>();

    private ExcelExportService CreateService(File file)
    {
        _fileService
            .GetAsync(Arg.Any<GetFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(file);
        return new ExcelExportService(_fileService);
    }

    private static ExportFileRequest CreateRequest() =>
        new() { FileId = FileId, AuthenticatedUserId = UserId };

    private static File CreateFile(params Expense[] expenses) =>
        new()
        {
            Id = FileId,
            UserId = UserId,
            ExtractionInfo = new ExtractionInfo { Expenses = [.. expenses] },
        };

    private static Expense CreateExpense(params LineItem[] lineItems) =>
        new()
        {
            VendorName = new ExtractedField { Text = VendorName },
            ReceiptDate = new DateField { Date = _receiptDate },
            Total = new AmountField { Amount = ExpenseTotal },
            LineItems = [.. lineItems],
        };

    private static LineItem CreateLineItem(string item, decimal? price) =>
        new()
        {
            Item = new ExtractedField { Text = item },
            Price = new AmountField { Amount = price },
        };

    private static async Task<IXLWorksheet> ExportWorksheet(ExcelExportService service)
    {
        var result = await service.ExportFile(CreateRequest(), CancellationToken.None);
        var workbook = new XLWorkbook(new MemoryStream(result.Content));
        return workbook.Worksheet(ExcelExportService.WorksheetName);
    }

    [Fact]
    public async Task ExportFile_ReturnsContentTypeAndFileName()
    {
        // Arrange
        var service = CreateService(CreateFile(CreateExpense()));

        // Act
        var result = await service.ExportFile(CreateRequest(), CancellationToken.None);

        // Assert
        result.ContentType.Should().Be(ExcelExportService.ExcelContentType);
        result.FileName.Should().Be($"{FileId}.xlsx");
    }

    [Fact]
    public async Task ExportFile_WritesBoldFrozenHeaderMatchingCsvColumns()
    {
        // Arrange
        var service = CreateService(CreateFile(CreateExpense()));

        // Act
        var worksheet = await ExportWorksheet(service);

        // Assert
        var headers = ExpenseExportTable.ColumnHeaders;
        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            var cell = worksheet.Cell(HeaderRow, columnIndex + 1);
            cell.GetString().Should().Be(headers[columnIndex]);
            cell.Style.Font.Bold.Should().BeTrue();
        }

        worksheet.SheetView.SplitRow.Should().Be(1);
    }

    [Fact]
    public async Task ExportFile_WithLineItems_WritesOneRowPerLineItemWithTypedCells()
    {
        // Arrange
        var service = CreateService(
            CreateFile(
                CreateExpense(CreateLineItem(ItemName, LineItemPrice), CreateLineItem("Soda", 1m))
            )
        );

        // Act
        var worksheet = await ExportWorksheet(service);

        // Assert
        worksheet.LastRowUsed()!.RowNumber().Should().Be(FirstDataRow + 1);
        var firstRow = worksheet.Row(FirstDataRow);
        firstRow.Cell(VendorNameColumn).GetString().Should().Be(VendorName);
        firstRow.Cell(ReceiptDateColumn).DataType.Should().Be(XLDataType.DateTime);
        firstRow
            .Cell(ReceiptDateColumn)
            .GetDateTime()
            .Should()
            .Be(_receiptDate.ToDateTime(TimeOnly.MinValue));
        firstRow
            .Cell(ReceiptDateColumn)
            .Style.DateFormat.Format.Should()
            .Be(ExcelExportService.DateNumberFormat);
        firstRow.Cell(TotalColumn).DataType.Should().Be(XLDataType.Number);
        firstRow.Cell(TotalColumn).GetDouble().Should().Be((double)ExpenseTotal);
        firstRow.Cell(LineItemItemColumn).GetString().Should().Be(ItemName);
        firstRow.Cell(LineItemPriceColumn).DataType.Should().Be(XLDataType.Number);
        firstRow.Cell(LineItemPriceColumn).GetDouble().Should().Be((double)LineItemPrice);
    }

    [Fact]
    public async Task ExportFile_WithMissingValues_UsesTextFallbackAndEmptyNumbers()
    {
        // Arrange
        var service = CreateService(CreateFile(CreateExpense(CreateLineItem(ItemName, null))));

        // Act
        var worksheet = await ExportWorksheet(service);

        // Assert
        var dataRow = worksheet.Row(FirstDataRow);
        dataRow.Cell(LineItemPriceColumn).IsEmpty().Should().BeTrue();
        dataRow.Cell(AmountPaidColumn).IsEmpty().Should().BeTrue();
        dataRow.Cell(ReceiptDateColumn + 1).IsEmpty().Should().BeTrue();
        dataRow.Cell(VendorNameColumn + 1).GetString().Should().Be(TextFallback);
    }

    [Fact]
    public async Task ExportFile_ExpenseWithoutLineItems_WritesSingleRow()
    {
        // Arrange
        var service = CreateService(CreateFile(CreateExpense()));

        // Act
        var worksheet = await ExportWorksheet(service);

        // Assert
        worksheet.LastRowUsed()!.RowNumber().Should().Be(FirstDataRow);
        worksheet.Cell(FirstDataRow, VendorNameColumn).GetString().Should().Be(VendorName);
        worksheet.Cell(FirstDataRow, LineItemItemColumn).GetString().Should().Be(TextFallback);
    }

    [Fact]
    public async Task ExportFile_WithoutExtraction_WritesHeaderOnly()
    {
        // Arrange
        var service = CreateService(new File { Id = FileId, UserId = UserId });

        // Act
        var worksheet = await ExportWorksheet(service);

        // Assert
        worksheet.LastRowUsed()!.RowNumber().Should().Be(HeaderRow);
    }
}
