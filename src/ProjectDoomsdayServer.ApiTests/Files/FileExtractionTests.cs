using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.Domain.Models.TextExtraction;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileExtractionTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string ExtractionSource = "test";
    private const string FirstVendorName = "first";
    private const string SecondVendorName = "second";

    private readonly CustomWebApplicationFactory _factory;

    public FileExtractionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _factory.FileExtractionServiceSubstitute.ClearReceivedCalls();
        _factory
            .FileExtractionServiceSubstitute.ExtractExpenseInformationAsync(
                Arg.Any<MemoryStream>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ => Task.FromResult(CreateExtraction(FirstVendorName)));
    }

    private static ExtractionInfo CreateExtraction(string vendorName) =>
        new()
        {
            ExtractedAtUtc = DateTimeOffset.UtcNow,
            Source = ExtractionSource,
            RawText = "raw",
            Expenses =
            [
                new Expense
                {
                    ExpenseIndex = 1,
                    VendorName = new ExtractedField { Type = "VENDOR_NAME", Text = vendorName },
                    Total = new AmountField
                    {
                        Type = "TOTAL",
                        Text = "11.52",
                        Amount = 11.52m,
                    },
                },
            ],
        };

    private async Task<File> CreateFileAsync(HttpClient client, bool withStorageKey = true)
    {
        var response = await client.PostAsJsonAsync(
            FileRoutes.Base,
            new CreateFileInput
            {
                FileName = TestConstants.ReceiptFileName,
                ContentType = TestConstants.ImageJpegContentType,
                SizeBytes = 10,
            }
        );
        response.EnsureSuccessStatusCode();
        var file = (await response.Content.ReadFromJsonAsync<CreateFileResult>())!.File;
        if (!withStorageKey)
            _factory.FileRecords[file.Id!].StorageKey = null;
        return file;
    }

    private Task<int> ExtractionCallCount() =>
        Task.FromResult(
            _factory
                .FileExtractionServiceSubstitute.ReceivedCalls()
                .Count(call =>
                    string.Equals(
                        call.GetMethodInfo().Name,
                        "ExtractExpenseInformationAsync",
                        StringComparison.Ordinal
                    )
                )
        );

    [Fact]
    public async Task Extract_Returns200WithTypedBody_AndStoresOnRecord()
    {
        // Arrange
        var client = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsync(client);

        // Act
        var response = await client.PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ExtractionInfo>();
        body!.Expenses.Single().Total!.Amount.Should().Be(11.52m);
        _factory.FileRecords[file.Id!].ExtractionInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task Extract_SecondCall_ReturnsCachedWithoutCallingService()
    {
        // Arrange
        var client = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsync(client);

        // Act
        await client.PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);
        var second = await client.PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ExtractionCallCount()).Should().Be(1);
    }

    [Fact]
    public async Task Extract_ForceTrue_CallsServiceAgainAndOverwrites()
    {
        // Arrange
        var client = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsync(client);
        await client.PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);
        _factory
            .FileExtractionServiceSubstitute.ExtractExpenseInformationAsync(
                Arg.Any<MemoryStream>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ => Task.FromResult(CreateExtraction(SecondVendorName)));

        // Act
        var response = await client.PostAsync(
            FileRoutes.ExtractExpenseInfo(file.Id!, force: true),
            null
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ExtractionCallCount()).Should().Be(2);
        _factory
            .FileRecords[file.Id!]
            .ExtractionInfo!.Expenses.Single()
            .VendorName!.Text.Should()
            .Be(SecondVendorName);
    }

    [Fact]
    public async Task Extract_FileOwnedByOtherUser_Returns404()
    {
        // Arrange
        var file = await CreateFileAsync(_factory.CreateClientAs(TestConstants.UserA));

        // Act
        var response = await _factory
            .CreateClientAs(TestConstants.UserB)
            .PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Extract_FileWithoutStorageKey_Returns404()
    {
        // Arrange
        var client = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsync(client, withStorageKey: false);

        // Act
        var response = await client.PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Extract_ZeroExpenses_StoresEmptyListAndCaches()
    {
        // Arrange
        _factory
            .FileExtractionServiceSubstitute.ExtractExpenseInformationAsync(
                Arg.Any<MemoryStream>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ => Task.FromResult(new ExtractionInfo { Source = ExtractionSource }));
        var client = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsync(client);

        // Act
        await client.PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);
        await client.PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);

        // Assert
        _factory.FileRecords[file.Id!].ExtractionInfo!.Expenses.Should().BeEmpty();
        (await ExtractionCallCount()).Should().Be(1);
    }

    [Fact]
    public async Task Extract_ServiceThrows_LeavesRecordUnchanged()
    {
        // Arrange
        _factory
            .FileExtractionServiceSubstitute.ExtractExpenseInformationAsync(
                Arg.Any<MemoryStream>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new InvalidOperationException("textract down"));
        var client = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsync(client);

        // Act
        var response = await client.PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        _factory.FileRecords[file.Id!].ExtractionInfo.Should().BeNull();
    }

    [Fact]
    public async Task Put_AfterExtraction_KeepsExtractionInfo()
    {
        // Arrange
        var client = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsync(client);
        await client.PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);

        // Act
        var put = await client.PutAsJsonAsync(
            FileRoutes.ById(file.Id!),
            new
            {
                FileName = "renamed.jpg",
                ContentType = TestConstants.ImageJpegContentType,
                SizeBytes = 10,
            }
        );

        // Assert
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.FileRecords[file.Id!].ExtractionInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAndList_IncludeExtractionInfo()
    {
        // Arrange
        var client = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsync(client);
        await client.PostAsync(FileRoutes.ExtractExpenseInfo(file.Id!), null);

        // Act
        var single = await client.GetFromJsonAsync<File>(FileRoutes.ById(file.Id!));
        var list = await client.GetFromJsonAsync<List<File>>(FileRoutes.List());

        // Assert
        single!.ExtractionInfo.Should().NotBeNull();
        list!.Single().ExtractionInfo.Should().NotBeNull();
    }
}
