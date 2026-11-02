using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.Domain.Models.TextExtraction;
using ProjectDoomsdayServer.E2ETests.Infrastructure;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.E2ETests.Files;

[Collection(E2ETestCollectionDefinition.CollectionName)]
public sealed class FileExtractionE2ETests : IAsyncLifetime
{
    private const string ImageJpegContentType = "image/jpeg";
    private const string MongoIdField = "_id";
    private const string ExtractionInfoField = "ExtractionInfo";
    private const string ExpensesField = "Expenses";

    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;

    public FileExtractionE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

    public Task InitializeAsync()
    {
        _factory = new E2EWebApplicationFactory(_infra);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static async Task<string> CreateUploadedFileAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            FileRoutes.Base,
            new CreateFileInput
            {
                FileName = TestConstants.ReceiptFileName,
                ContentType = ImageJpegContentType,
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
            .GetCollection<BsonDocument>(TestConstants.FilesCollectionName);

    [Fact]
    public async Task Extract_ThenGet_RoundTripsFullSubdocumentIncludingGeometry()
    {
        // Arrange
        var client = _factory.CreateClientAs(E2ETestHelpers.UniqueUserId());
        var fileId = await CreateUploadedFileAsync(client);

        // Act
        var extract = await client.PostAsync(FileRoutes.ExtractExpenseInfo(fileId), null);
        var extracted = await extract.Content.ReadFromJsonAsync<ExtractionInfo>();
        var fetched = await client.GetFromJsonAsync<File>(FileRoutes.ById(fileId));

        // Assert
        extract.StatusCode.Should().Be(HttpStatusCode.OK);
        fetched!.ExtractionInfo.Should().BeEquivalentTo(extracted);
        fetched.ExtractionInfo!.Expenses.Single().Total!.Geometry!.Polygon.Should().HaveCount(4);
    }

    [Fact]
    public async Task Extract_StoresAmountsAsDecimal128()
    {
        // Arrange
        var client = _factory.CreateClientAs(E2ETestHelpers.UniqueUserId());
        var fileId = await CreateUploadedFileAsync(client);

        // Act
        await client.PostAsync(FileRoutes.ExtractExpenseInfo(fileId), null);

        // Assert
        var document = await RawCollection()
            .Find(new BsonDocument(MongoIdField, fileId))
            .SingleAsync();
        var amount = document[ExtractionInfoField][ExpensesField][0]["Total"]["Amount"];
        amount.BsonType.Should().Be(BsonType.Decimal128);
        amount.AsDecimal.Should().Be(11.52m);
    }

    [Fact]
    public async Task Extract_SecondCallFromNewFactory_ServesPersistedResult()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var fileId = await CreateUploadedFileAsync(_factory.CreateClientAs(userId));
        await _factory
            .CreateClientAs(userId)
            .PostAsync(FileRoutes.ExtractExpenseInfo(fileId), null);
        await using var restartedFactory = new E2EWebApplicationFactory(_infra);

        // Act
        var response = await restartedFactory
            .CreateClientAs(userId)
            .PostAsync(FileRoutes.ExtractExpenseInfo(fileId), null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        restartedFactory.ExtractionService.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Put_AfterExtract_PreservesSubdocumentInMongo()
    {
        // Arrange
        var client = _factory.CreateClientAs(E2ETestHelpers.UniqueUserId());
        var fileId = await CreateUploadedFileAsync(client);
        await client.PostAsync(FileRoutes.ExtractExpenseInfo(fileId), null);

        // Act
        var put = await client.PutAsJsonAsync(
            FileRoutes.ById(fileId),
            new
            {
                FileName = "renamed.jpg",
                ContentType = ImageJpegContentType,
                SizeBytes = 5,
            }
        );

        // Assert
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = await RawCollection()
            .Find(new BsonDocument(MongoIdField, fileId))
            .SingleAsync();
        document[ExtractionInfoField][ExpensesField].AsBsonArray.Should().HaveCount(1);
    }
}
