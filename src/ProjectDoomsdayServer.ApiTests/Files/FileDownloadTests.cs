using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileDownloadTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileDownloadTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _client = factory.CreateClient();
    }

    private async Task<File> UpsertTestFile(
        string fileName = "test.txt",
        string contentType = "text/plain",
        long sizeBytes = 100
    )
    {
        var record = new File
        {
            Id = Guid.NewGuid().ToString("N"),
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
        };
        var response = await _client.PostAsJsonAsync("/files", record);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<File>())!;
    }

    [Fact]
    public async Task Download_ExistingFile_ReturnsFileStream()
    {
        // Arrange
        var record = await UpsertTestFile();

        // Act
        var response = await _client.GetAsync($"/files/{record.Id}/content");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_ExistingFile_HasCorrectContentType()
    {
        // Arrange
        var record = await UpsertTestFile("document.pdf", "application/pdf");

        // Act
        var response = await _client.GetAsync($"/files/{record.Id}/content");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Download_ExistingFile_HasCorrectFileName()
    {
        // Arrange
        var record = await UpsertTestFile("report.pdf", "application/pdf");

        // Act
        var response = await _client.GetAsync($"/files/{record.Id}/content");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contentDisposition = response.Content.Headers.ContentDisposition;
        contentDisposition.Should().NotBeNull();
        contentDisposition!.FileName.Should().Contain("report.pdf");
    }

    [Fact]
    public async Task Download_NonExistingFile_Returns404()
    {
        // Arrange
        var randomId = Guid.NewGuid().ToString("N");

        // Act
        var response = await _client.GetAsync($"/files/{randomId}/content");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
