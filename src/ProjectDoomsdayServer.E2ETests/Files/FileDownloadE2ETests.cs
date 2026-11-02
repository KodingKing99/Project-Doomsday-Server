using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.E2ETests.Infrastructure;
using ProjectDoomsdayServer.TestSupport;

namespace ProjectDoomsdayServer.E2ETests.Files;

[Collection(E2ETestCollectionDefinition.CollectionName)]
public sealed class FileDownloadE2ETests : IAsyncLifetime
{
    private const string PdfReportFileName = "report.pdf";
    private const string NonExistentFileId = "000000000000000000000000";

    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;
    private readonly HttpClient _rawHttpClient = new();

    public FileDownloadE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

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
    public async Task Download_NonExistentFile_Returns404()
    {
        // Arrange
        var client = _factory.CreateClientAs(E2ETestHelpers.UniqueUserId());

        // Act
        var response = await client.GetAsync(FileRoutes.Content(NonExistentFileId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_ContentTypeHeader_MatchesCreatedRecord()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        var content = E2ETestHelpers.Utf8Bytes("<html><body>hello</body></html>");
        var record = new CreateFileInput
        {
            FileName = "page.html",
            ContentType = TestConstants.HtmlContentType,
            SizeBytes = content.Length,
        };
        var createResponse = await client.PostAsJsonAsync(FileRoutes.Base, record);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = createResult!.File.Id!;
        var uploadUrl = createResult.UploadUrl;
        using var putContent = new ByteArrayContent(content);
        await _rawHttpClient.PutAsync(uploadUrl, putContent);

        // Act
        var downloadResponse = await client.GetAsync(FileRoutes.Content(fileId));

        // Assert
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        downloadResponse
            .Content.Headers.ContentType!.MediaType.Should()
            .Be(TestConstants.HtmlContentType);
    }

    [Fact]
    public async Task Download_ContentDispositionHeader_ContainsFileName()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        var content = E2ETestHelpers.Utf8Bytes("attachment test content");
        var record = new CreateFileInput
        {
            FileName = PdfReportFileName,
            ContentType = "application/pdf",
            SizeBytes = content.Length,
        };
        var createResponse = await client.PostAsJsonAsync(FileRoutes.Base, record);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = createResult!.File.Id!;
        var uploadUrl = createResult.UploadUrl;
        using var putContent = new ByteArrayContent(content);
        await _rawHttpClient.PutAsync(uploadUrl, putContent);

        // Act
        var downloadResponse = await client.GetAsync(FileRoutes.Content(fileId));

        // Assert
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var disposition = downloadResponse.Content.Headers.ContentDisposition;
        disposition.Should().NotBeNull();
        disposition!.FileName.Should().NotBeNullOrEmpty();
        disposition.FileName.Should().Contain(PdfReportFileName);
    }
}
