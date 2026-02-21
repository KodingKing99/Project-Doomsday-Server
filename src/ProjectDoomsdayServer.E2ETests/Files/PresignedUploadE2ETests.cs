using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.E2ETests.Infrastructure;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.E2ETests.Files;

[Collection(E2ETestCollection.CollectionName)]
public sealed class PresignedUploadE2ETests : IAsyncLifetime
{
    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;
    private HttpClient _appClient = default!;
    private readonly HttpClient _rawHttpClient = new();

    public PresignedUploadE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

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
    public async Task TextContent_RoundTrip_BytesMatchAfterDownload()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var content = E2ETestHelpers.Utf8Bytes("Hello, E2E world!");

        // POST /files → get presigned upload URL
        var record = new File
        {
            UserId = userId,
            FileName = "hello.txt",
            ContentType = "text/plain",
            SizeBytes = content.Length,
        };
        var createResponse = await _appClient.PostAsJsonAsync("/files", record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult.Should().NotBeNull();
        var uploadUrl = createResult!.UploadUrl;
        var fileId = createResult.File.Id;

        // PUT bytes directly to presigned MinIO URL
        using var putContent = new ByteArrayContent(content);
        var putResponse = await _rawHttpClient.PutAsync(uploadUrl, putContent);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET /files/{id}/content → verify bytes match
        var downloadResponse = await _appClient.GetAsync($"/files/{fileId}/content");
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(content);
    }

    [Fact]
    public async Task LargeBinary_512KB_RoundTrip_BytesMatchAfterDownload()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var content = E2ETestHelpers.RandomBytes(512 * 1024);

        var record = new File
        {
            UserId = userId,
            FileName = "binary.bin",
            ContentType = "application/octet-stream",
            SizeBytes = content.Length,
        };
        var createResponse = await _appClient.PostAsJsonAsync("/files", record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var uploadUrl = createResult!.UploadUrl;
        var fileId = createResult.File.Id;

        using var putContent = new ByteArrayContent(content);
        var putResponse = await _rawHttpClient.PutAsync(uploadUrl, putContent);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var downloadResponse = await _appClient.GetAsync($"/files/{fileId}/content");
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(content);
    }

    [Fact]
    public async Task Metadata_PersistedToMongoDB_ReturnedOnGet()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var record = new File
        {
            UserId = userId,
            FileName = "meta.txt",
            ContentType = "text/plain",
            SizeBytes = 10,
            Metadata = { ["env"] = "e2e", ["version"] = "42" },
        };

        var createResponse = await _appClient.PostAsJsonAsync("/files", record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = createResult!.File.Id;

        var getResponse = await _appClient.GetAsync($"/files/{fileId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<File>();
        fetched.Should().NotBeNull();
        fetched!.Metadata.Should().ContainKey("env").WhoseValue.Should().Be("e2e");
        fetched.Metadata.Should().ContainKey("version").WhoseValue.Should().Be("42");
    }
}
