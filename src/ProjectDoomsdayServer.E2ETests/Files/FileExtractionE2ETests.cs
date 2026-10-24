using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Extraction;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.E2ETests.Infrastructure;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.E2ETests.Files;

[Collection(E2ETestCollection.CollectionName)]
public sealed class FileExtractionE2ETests : IAsyncLifetime
{
    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;

    public FileExtractionE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

    public Task InitializeAsync()
    {
        _factory = new E2EWebApplicationFactory(_infra);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<string> CreateUploadedFileAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/files",
            new CreateFileInput
            {
                FileName = "receipt.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 5,
            }
        );
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<CreateFileResult>())!;
        using var rawClient = new HttpClient();
        var put = await rawClient.PutAsync(
            result.UploadUrl,
            new ByteArrayContent(E2ETestHelpers.Utf8Bytes("bytes"))
        );
        put.EnsureSuccessStatusCode();
        return result.File.Id!;
    }

    private IMongoCollection<BsonDocument> RawCollection() =>
        new MongoClient(_infra.MongoConnectionString)
            .GetDatabase(_infra.DatabaseName)
            .GetCollection<BsonDocument>("files");

    [Fact]
    public async Task Extract_ThenGet_RoundTripsFullSubdocumentIncludingGeometry()
    {
        var client = _factory.CreateClientAs(E2ETestHelpers.UniqueUserId());
        var fileId = await CreateUploadedFileAsync(client);

        var extract = await client.PostAsync($"/files/{fileId}/extractExpenseInfo", null);
        var extracted = await extract.Content.ReadFromJsonAsync<ExtractedExpenseInfo>();
        var fetched = await client.GetFromJsonAsync<File>($"/files/{fileId}");

        extract.StatusCode.Should().Be(HttpStatusCode.OK);
        fetched!.ExtractedExpenseInfo.Should().BeEquivalentTo(extracted);
        fetched
            .ExtractedExpenseInfo!.Expenses.Single()
            .Total!.Geometry!.Polygon.Should()
            .HaveCount(4);
    }

    [Fact]
    public async Task Extract_StoresAmountsAsDecimal128()
    {
        var client = _factory.CreateClientAs(E2ETestHelpers.UniqueUserId());
        var fileId = await CreateUploadedFileAsync(client);

        await client.PostAsync($"/files/{fileId}/extractExpenseInfo", null);

        var document = await RawCollection().Find(new BsonDocument("_id", fileId)).SingleAsync();
        var amount = document["ExtractedExpenseInfo"]["Expenses"][0]["Total"]["Amount"];
        amount.BsonType.Should().Be(BsonType.Decimal128);
        amount.AsDecimal.Should().Be(11.52m);
    }

    [Fact]
    public async Task Extract_SecondCallFromNewFactory_ServesPersistedResult()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var fileId = await CreateUploadedFileAsync(_factory.CreateClientAs(userId));
        await _factory
            .CreateClientAs(userId)
            .PostAsync($"/files/{fileId}/extractExpenseInfo", null);

        await using var restartedFactory = new E2EWebApplicationFactory(_infra);
        var response = await restartedFactory
            .CreateClientAs(userId)
            .PostAsync($"/files/{fileId}/extractExpenseInfo", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        restartedFactory.ExtractionService.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Put_AfterExtract_PreservesSubdocumentInMongo()
    {
        var client = _factory.CreateClientAs(E2ETestHelpers.UniqueUserId());
        var fileId = await CreateUploadedFileAsync(client);
        await client.PostAsync($"/files/{fileId}/extractExpenseInfo", null);

        var put = await client.PutAsJsonAsync(
            $"/files/{fileId}",
            new
            {
                FileName = "renamed.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 5,
            }
        );

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = await RawCollection().Find(new BsonDocument("_id", fileId)).SingleAsync();
        document["ExtractedExpenseInfo"]["Expenses"].AsBsonArray.Should().HaveCount(1);
    }
}
