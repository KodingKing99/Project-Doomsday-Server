using System.Net;
using System.Net.Http.Json;
using Amazon.S3;
using FluentAssertions;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.E2ETests.Infrastructure;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.E2ETests.Files;

[Collection(E2ETestCollection.CollectionName)]
public sealed class FileCrudE2ETests : IAsyncLifetime
{
    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;
    private HttpClient _appClient = default!;
    private readonly HttpClient _rawHttpClient = new();

    public FileCrudE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

    public Task InitializeAsync()
    {
        _factory = new E2EWebApplicationFactory(_infra);
        _appClient = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _appClient.Dispose();
        _rawHttpClient.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Create_PersistedToMongoDB_GetReturnsRecord()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var record = new File
        {
            UserId = userId,
            FileName = "test.txt",
            ContentType = "text/plain",
            SizeBytes = 100,
        };

        var createResponse = await _appClient.PostAsJsonAsync("/files", record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult.Should().NotBeNull();
        var fileId = createResult!.File.Id;

        var getResponse = await _appClient.GetAsync($"/files/{fileId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<File>();
        fetched.Should().NotBeNull();
        fetched!.FileName.Should().Be("test.txt");
        fetched.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task Create_StorageKeyFormat_IsUserIdSlashFileId()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var record = new File
        {
            UserId = userId,
            FileName = "key-test.txt",
            ContentType = "text/plain",
            SizeBytes = 10,
        };

        var createResponse = await _appClient.PostAsJsonAsync("/files", record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var file = createResult!.File;

        file.StorageKey.Should().Be($"{userId}/{file.Id}");
    }

    [Fact]
    public async Task Create_UploadUrl_StartsWithHttpScheme()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var record = new File
        {
            UserId = userId,
            FileName = "url-test.txt",
            ContentType = "text/plain",
            SizeBytes = 10,
        };

        var createResponse = await _appClient.PostAsJsonAsync("/files", record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();

        createResult!.UploadUrl.Should().StartWith("http://");
    }

    [Fact]
    public async Task Update_ChangesFieldsInMongoDB()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var record = new File
        {
            UserId = userId,
            FileName = "original.txt",
            ContentType = "text/plain",
            SizeBytes = 50,
        };

        var createResponse = await _appClient.PostAsJsonAsync("/files", record);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = createResult!.File.Id;

        var updatedRecord = new File
        {
            UserId = userId,
            FileName = "updated.txt",
            ContentType = "text/html",
            SizeBytes = 100,
        };
        var updateResponse = await _appClient.PutAsJsonAsync($"/files/{fileId}", updatedRecord);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _appClient.GetAsync($"/files/{fileId}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<File>();
        fetched!.FileName.Should().Be("updated.txt");
        fetched.ContentType.Should().Be("text/html");
        fetched.SizeBytes.Should().Be(100);
    }

    [Fact]
    public async Task Delete_RemovesRecordFromMongoDB_AndObjectFromMinIO()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var content = E2ETestHelpers.Utf8Bytes("delete me");
        var record = new File
        {
            UserId = userId,
            FileName = "to-delete.txt",
            ContentType = "text/plain",
            SizeBytes = content.Length,
        };

        // Create and upload
        var createResponse = await _appClient.PostAsJsonAsync("/files", record);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = createResult!.File.Id;
        var uploadUrl = createResult.UploadUrl;
        var storageKey = createResult.File.StorageKey;

        using var putContent = new ByteArrayContent(content);
        await _rawHttpClient.PutAsync(uploadUrl, putContent);

        // Delete via API
        var deleteResponse = await _appClient.DeleteAsync($"/files/{fileId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // MongoDB record is gone
        var getResponse = await _appClient.GetAsync($"/files/{fileId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // MinIO object is gone
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
        var userId = E2ETestHelpers.UniqueUserId();

        // Create 3 files sequentially with small delays to ensure distinct timestamps
        for (var i = 1; i <= 3; i++)
        {
            var r = new File
            {
                UserId = userId,
                FileName = $"file{i}.txt",
                ContentType = "text/plain",
                SizeBytes = i * 100,
            };
            var resp = await _appClient.PostAsJsonAsync("/files", r);
            resp.StatusCode.Should().Be(HttpStatusCode.Created);
            await Task.Delay(25);
        }

        // List all and filter client-side by our unique userId
        var listResponse = await _appClient.GetAsync("/files?skip=0&take=200");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var all = await listResponse.Content.ReadFromJsonAsync<List<File>>();
        var myFiles = all!.Where(f => f.UserId == userId).ToList();

        myFiles.Should().HaveCount(3);

        // Newest-first: file3 > file2 > file1
        myFiles[0].UpdatedAtUtc.Should().BeOnOrAfter(myFiles[1].UpdatedAtUtc);
        myFiles[1].UpdatedAtUtc.Should().BeOnOrAfter(myFiles[2].UpdatedAtUtc);

        // Pagination: skip=1 take=1 returns exactly 1 item from the global list
        var pageResponse = await _appClient.GetAsync("/files?skip=1&take=1");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await pageResponse.Content.ReadFromJsonAsync<List<File>>();
        page.Should().HaveCount(1);
    }
}
