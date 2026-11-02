using System.Net;
using System.Net.Http.Json;
using Amazon.S3;
using FluentAssertions;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.E2ETests.Infrastructure;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.E2ETests.Files;

[Collection(E2ETestCollectionDefinition.CollectionName)]
public sealed class FileCrudE2ETests : IAsyncLifetime
{
    private const string DefaultFileName = "test.txt";
    private const string UpdatedFileName = "updated.txt";
    private const int DefaultSizeBytes = 100;

    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;
    private readonly HttpClient _rawHttpClient = new();

    public FileCrudE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

    public Task InitializeAsync()
    {
        _factory = new E2EWebApplicationFactory(_infra);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _rawHttpClient.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Create_PersistedToMongoDB_GetReturnsRecord()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        var record = new CreateFileInput
        {
            FileName = DefaultFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = DefaultSizeBytes,
        };

        // Act & Assert - create, then get
        var createResponse = await client.PostAsJsonAsync(FileRoutes.Base, record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult.Should().NotBeNull();
        var fileId = createResult!.File.Id!;
        var getResponse = await client.GetAsync(FileRoutes.ById(fileId));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<File>();
        fetched.Should().NotBeNull();
        fetched!.FileName.Should().Be(DefaultFileName);
        fetched.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task Create_StorageKeyFormat_IsUserIdSlashFileId()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        var record = new CreateFileInput
        {
            FileName = "key-test.txt",
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 10,
        };

        // Act
        var createResponse = await client.PostAsJsonAsync(FileRoutes.Base, record);

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var file = createResult!.File;
        file.StorageKey.Should().Be($"{userId}/{file.Id}");
    }

    [Fact]
    public async Task Create_UploadUrl_StartsWithHttpScheme()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        var record = new CreateFileInput
        {
            FileName = "url-test.txt",
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 10,
        };

        // Act
        var createResponse = await client.PostAsJsonAsync(FileRoutes.Base, record);

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult!.UploadUrl.Should().StartWith("http://");
    }

    [Fact]
    public async Task Update_ChangesFieldsInMongoDB()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        var record = new CreateFileInput
        {
            FileName = "original.txt",
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 50,
        };
        var createResponse = await client.PostAsJsonAsync(FileRoutes.Base, record);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = createResult!.File.Id!;
        var updatedRecord = new File
        {
            UserId = userId,
            FileName = UpdatedFileName,
            ContentType = TestConstants.HtmlContentType,
            SizeBytes = DefaultSizeBytes,
        };

        // Act & Assert - update, then get
        var updateResponse = await client.PutAsJsonAsync(FileRoutes.ById(fileId), updatedRecord);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResponse = await client.GetAsync(FileRoutes.ById(fileId));
        var fetched = await getResponse.Content.ReadFromJsonAsync<File>();
        fetched!.FileName.Should().Be(UpdatedFileName);
        fetched.ContentType.Should().Be(TestConstants.HtmlContentType);
        fetched.SizeBytes.Should().Be(DefaultSizeBytes);
    }

    [Fact]
    public async Task Delete_RemovesRecordFromMongoDB_AndObjectFromMinIO()
    {
        // Arrange - create and upload
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        var content = E2ETestHelpers.Utf8Bytes("delete me");
        var record = new CreateFileInput
        {
            FileName = "to-delete.txt",
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = content.Length,
        };
        var createResponse = await client.PostAsJsonAsync(FileRoutes.Base, record);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = createResult!.File.Id!;
        var uploadUrl = createResult.UploadUrl;
        var storageKey = createResult.File.StorageKey;
        using var putContent = new ByteArrayContent(content);
        await _rawHttpClient.PutAsync(uploadUrl, putContent);

        // Act & Assert - delete via API, then verify the MongoDB record and MinIO object are gone
        var deleteResponse = await client.DeleteAsync(FileRoutes.ById(fileId));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await client.GetAsync(FileRoutes.ById(fileId));
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var s3 = _infra.BuildS3Client();
        var act = async () =>
            await s3.GetObjectMetadataAsync(E2EInfrastructureFixture.BucketName, storageKey);
        await act.Should()
            .ThrowAsync<AmazonS3Exception>()
            .Where(ex => ex.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_ReturnsNewestFirst_PaginationWorks()
    {
        // Arrange - create 3 files sequentially with small delays to ensure distinct timestamps
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        for (var i = 1; i <= 3; i++)
        {
            var r = new CreateFileInput
            {
                FileName = $"file{i}.txt",
                ContentType = TestConstants.PlainTextContentType,
                SizeBytes = i * 100,
            };
            var resp = await client.PostAsJsonAsync(FileRoutes.Base, r);
            resp.StatusCode.Should().Be(HttpStatusCode.Created);
            await Task.Delay(25);
        }

        // Act & Assert - list all (the API returns only this user's files), newest first
        var listResponse = await client.GetAsync(FileRoutes.List(skip: 0, take: 200));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var files = await listResponse.Content.ReadFromJsonAsync<List<File>>();
        files.Should().HaveCount(3);
        files![0].UpdatedAtUtc.Should().BeOnOrAfter(files[1].UpdatedAtUtc);
        files[1].UpdatedAtUtc.Should().BeOnOrAfter(files[2].UpdatedAtUtc);

        // Act & Assert - skip=1 take=1 returns the 2nd item from this user's list
        var pageResponse = await client.GetAsync(FileRoutes.List(skip: 1, take: 1));
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await pageResponse.Content.ReadFromJsonAsync<List<File>>();
        page.Should().HaveCount(1);
    }
}
