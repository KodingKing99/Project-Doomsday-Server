using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Extraction;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileExtractionTests : IClassFixture<CustomWebApplicationFactory>
{
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
            .Returns(_ => Task.FromResult(CreateExtraction("first")));
    }

    private static ExtractedExpenseInfo CreateExtraction(string vendorName) =>
        new()
        {
            ExtractedAtUtc = DateTimeOffset.UtcNow,
            Source = "test",
            RawText = "raw",
            Expenses =
            [
                new Expense
                {
                    ExpenseIndex = 1,
                    VendorName = new ExtractedField { Type = "VENDOR_NAME", Text = vendorName },
                    Total = new ExtractedAmountField
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
            "/files",
            new CreateFileInput
            {
                FileName = "receipt.jpg",
                ContentType = "image/jpeg",
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
                .Count(call => call.GetMethodInfo().Name == "ExtractExpenseInformationAsync")
        );

    [Fact]
    public async Task Extract_Returns200WithTypedBody_AndStoresOnRecord()
    {
        var client = _factory.CreateClientAs("user-a");
        var file = await CreateFileAsync(client);

        var response = await client.PostAsync($"/files/{file.Id}/extractExpenseInfo", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ExtractedExpenseInfo>();
        body!.Expenses.Single().Total!.Amount.Should().Be(11.52m);
        _factory.FileRecords[file.Id!].ExtractedExpenseInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task Extract_SecondCall_ReturnsCachedWithoutCallingService()
    {
        var client = _factory.CreateClientAs("user-a");
        var file = await CreateFileAsync(client);

        await client.PostAsync($"/files/{file.Id}/extractExpenseInfo", null);
        var second = await client.PostAsync($"/files/{file.Id}/extractExpenseInfo", null);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ExtractionCallCount()).Should().Be(1);
    }

    [Fact]
    public async Task Extract_ForceTrue_CallsServiceAgainAndOverwrites()
    {
        var client = _factory.CreateClientAs("user-a");
        var file = await CreateFileAsync(client);
        await client.PostAsync($"/files/{file.Id}/extractExpenseInfo", null);
        _factory
            .FileExtractionServiceSubstitute.ExtractExpenseInformationAsync(
                Arg.Any<MemoryStream>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ => Task.FromResult(CreateExtraction("second")));

        var response = await client.PostAsync(
            $"/files/{file.Id}/extractExpenseInfo?force=true",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ExtractionCallCount()).Should().Be(2);
        _factory
            .FileRecords[file.Id!]
            .ExtractedExpenseInfo!.Expenses.Single()
            .VendorName!.Text.Should()
            .Be("second");
    }

    [Fact]
    public async Task Extract_FileOwnedByOtherUser_Returns404()
    {
        var file = await CreateFileAsync(_factory.CreateClientAs("user-a"));

        var response = await _factory
            .CreateClientAs("user-b")
            .PostAsync($"/files/{file.Id}/extractExpenseInfo", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Extract_FileWithoutStorageKey_Returns404()
    {
        var client = _factory.CreateClientAs("user-a");
        var file = await CreateFileAsync(client, withStorageKey: false);

        var response = await client.PostAsync($"/files/{file.Id}/extractExpenseInfo", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Extract_ZeroExpenses_StoresEmptyListAndCaches()
    {
        _factory
            .FileExtractionServiceSubstitute.ExtractExpenseInformationAsync(
                Arg.Any<MemoryStream>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ => Task.FromResult(new ExtractedExpenseInfo { Source = "test" }));
        var client = _factory.CreateClientAs("user-a");
        var file = await CreateFileAsync(client);

        await client.PostAsync($"/files/{file.Id}/extractExpenseInfo", null);
        await client.PostAsync($"/files/{file.Id}/extractExpenseInfo", null);

        _factory.FileRecords[file.Id!].ExtractedExpenseInfo!.Expenses.Should().BeEmpty();
        (await ExtractionCallCount()).Should().Be(1);
    }

    [Fact]
    public async Task Extract_ServiceThrows_LeavesRecordUnchanged()
    {
        _factory
            .FileExtractionServiceSubstitute.ExtractExpenseInformationAsync(
                Arg.Any<MemoryStream>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new InvalidOperationException("textract down"));
        var client = _factory.CreateClientAs("user-a");
        var file = await CreateFileAsync(client);

        var response = await client.PostAsync($"/files/{file.Id}/extractExpenseInfo", null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        _factory.FileRecords[file.Id!].ExtractedExpenseInfo.Should().BeNull();
    }

    [Fact]
    public async Task Put_AfterExtraction_KeepsExtractedExpenseInfo()
    {
        var client = _factory.CreateClientAs("user-a");
        var file = await CreateFileAsync(client);
        await client.PostAsync($"/files/{file.Id}/extractExpenseInfo", null);

        var put = await client.PutAsJsonAsync(
            $"/files/{file.Id}",
            new
            {
                FileName = "renamed.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 10,
            }
        );

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.FileRecords[file.Id!].ExtractedExpenseInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAndList_IncludeExtractedExpenseInfo()
    {
        var client = _factory.CreateClientAs("user-a");
        var file = await CreateFileAsync(client);
        await client.PostAsync($"/files/{file.Id}/extractExpenseInfo", null);

        var single = await client.GetFromJsonAsync<File>($"/files/{file.Id}");
        var list = await client.GetFromJsonAsync<List<File>>("/files");

        single!.ExtractedExpenseInfo.Should().NotBeNull();
        list!.Single().ExtractedExpenseInfo.Should().NotBeNull();
    }
}
