using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.DbModels;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileDownloadTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PdfContentType = "application/pdf";
    private const string ReportFileName = "report.pdf";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileDownloadTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _client = factory.CreateClient();
    }

    private async Task<File> CreateTestFile(
        string fileName = TestConstants.DefaultTestFileName,
        string contentType = TestConstants.PlainTextContentType,
        long sizeBytes = 100
    )
    {
        var record = new File
        {
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
        };
        var response = await _client.PostAsJsonAsync(FileRoutes.Base, record);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        return result!.File;
    }

    [Fact]
    public async Task Download_ExistingFile_ReturnsFileStream()
    {
        // Arrange
        var record = await CreateTestFile();

        // Act
        var response = await _client.GetAsync(FileRoutes.Content(record.Id!));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_ExistingFile_HasCorrectContentType()
    {
        // Arrange
        var record = await CreateTestFile("document.pdf", PdfContentType);

        // Act
        var response = await _client.GetAsync(FileRoutes.Content(record.Id!));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be(PdfContentType);
    }

    [Fact]
    public async Task Download_ExistingFile_HasCorrectFileName()
    {
        // Arrange
        var record = await CreateTestFile(ReportFileName, PdfContentType);

        // Act
        var response = await _client.GetAsync(FileRoutes.Content(record.Id!));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contentDisposition = response.Content.Headers.ContentDisposition;
        contentDisposition.Should().NotBeNull();
        contentDisposition!.FileName.Should().Contain(ReportFileName);
    }

    [Fact]
    public async Task Download_NonExistingFile_Returns404()
    {
        // Arrange
        var randomId = Guid.NewGuid().ToString("N");

        // Act
        var response = await _client.GetAsync(FileRoutes.Content(randomId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
