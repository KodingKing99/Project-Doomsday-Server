using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.E2ETests.Infrastructure;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.E2ETests.Files;

[Collection(E2ETestCollection.CollectionName)]
public sealed class FileDownloadE2ETests : IAsyncLifetime
{
    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;
    private HttpClient _appClient = default!;
    private readonly HttpClient _rawHttpClient = new();

    public FileDownloadE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

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
    public async Task Download_NonExistentFile_Returns404()
    {
        var response = await _appClient.GetAsync("/files/000000000000000000000000/content");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_ContentTypeHeader_MatchesCreatedRecord()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var content = E2ETestHelpers.Utf8Bytes("<html><body>hello</body></html>");
        var record = new File
        {
            UserId = userId,
            FileName = "page.html",
            ContentType = "text/html",
            SizeBytes = content.Length,
        };

        var createResponse = await _appClient.PostAsJsonAsync("/files", record);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = createResult!.File.Id;
        var uploadUrl = createResult.UploadUrl;

        using var putContent = new ByteArrayContent(content);
        await _rawHttpClient.PutAsync(uploadUrl, putContent);

        var downloadResponse = await _appClient.GetAsync($"/files/{fileId}/content");
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        downloadResponse.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task Download_ContentDispositionHeader_ContainsFileName()
    {
        var userId = E2ETestHelpers.UniqueUserId();
        var content = E2ETestHelpers.Utf8Bytes("attachment test content");
        var record = new File
        {
            UserId = userId,
            FileName = "report.pdf",
            ContentType = "application/pdf",
            SizeBytes = content.Length,
        };

        var createResponse = await _appClient.PostAsJsonAsync("/files", record);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = createResult!.File.Id;
        var uploadUrl = createResult.UploadUrl;

        using var putContent = new ByteArrayContent(content);
        await _rawHttpClient.PutAsync(uploadUrl, putContent);

        var downloadResponse = await _appClient.GetAsync($"/files/{fileId}/content");
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var disposition = downloadResponse.Content.Headers.ContentDisposition;
        disposition.Should().NotBeNull();
        disposition!.FileName.Should().NotBeNullOrEmpty();
        disposition.FileName.Should().Contain("report.pdf");
    }
}
