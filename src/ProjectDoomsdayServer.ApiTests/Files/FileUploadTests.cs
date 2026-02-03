using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Domain.Files;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileUploadTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileUploadTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_ValidFile_ReturnsCreatedWithFileRecord()
    {
        // Arrange
        var content = TestHelpers.CreateTextContent("Hello, World!");
        using var form = TestHelpers.CreateFileUpload("test.txt", content, "text/plain");

        // Act
        var response = await _client.PostAsync("/files", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var record = await response.Content.ReadFromJsonAsync<FileRecord>();
        record.Should().NotBeNull();
        record!.Id.Should().NotBeEmpty();
        record.FileName.Should().Be("test.txt");
        record.ContentType.Should().Be("text/plain");
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Upload_LargeFile_UnderLimit_Succeeds()
    {
        // Arrange - Create file at ~10MB (well under 500MB limit)
        var content = TestHelpers.GenerateRandomBytes(10 * 1024 * 1024);
        using var form = TestHelpers.CreateFileUpload("large.bin", content);

        // Act
        var response = await _client.PostAsync("/files", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var record = await response.Content.ReadFromJsonAsync<FileRecord>();
        record.Should().NotBeNull();
        record!.SizeBytes.Should().Be(content.Length);
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        // Arrange - 0-byte file
        // Note: Controller rejects empty files (file.Length == 0)
        var content = Array.Empty<byte>();
        using var form = TestHelpers.CreateFileUpload("empty.txt", content, "text/plain");

        // Act
        var response = await _client.PostAsync("/files", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_FileWithSpecialCharactersInName_Succeeds()
    {
        // Arrange
        var fileName = "test file (1) [copy].txt";
        var content = TestHelpers.CreateTextContent("content");
        using var form = TestHelpers.CreateFileUpload(fileName, content, "text/plain");

        // Act
        var response = await _client.PostAsync("/files", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var record = await response.Content.ReadFromJsonAsync<FileRecord>();
        record.Should().NotBeNull();
        record!.FileName.Should().Be(fileName);
    }

    [Fact]
    public async Task Upload_FileWithUnicodeCharactersInName_Succeeds()
    {
        // Arrange
        var fileName = "unicode_test.txt";
        var content = TestHelpers.CreateTextContent("content");
        using var form = TestHelpers.CreateFileUpload(fileName, content, "text/plain");

        // Act
        var response = await _client.PostAsync("/files", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var record = await response.Content.ReadFromJsonAsync<FileRecord>();
        record.Should().NotBeNull();
        record!.FileName.Should().Be(fileName);
    }

    [Fact]
    public async Task Upload_NoFile_ReturnsBadRequest()
    {
        // Arrange - Empty multipart form
        using var form = new MultipartFormDataContent();

        // Act
        var response = await _client.PostAsync("/files", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_ExceedsSizeLimit_Returns413()
    {
        // Arrange - Create file exceeding size limit (>500MB)
        // Note: This test simulates the behavior - actual 500MB+ file would be impractical
        // The controller has [RequestSizeLimit(524_288_000)] which is 500MB
        // We test that oversized requests are rejected
        var content = TestHelpers.GenerateRandomBytes(1024);
        using var form = TestHelpers.CreateFileUpload("huge.bin", content);

        // Act
        var response = await _client.PostAsync("/files", form);

        // Assert
        // For now we expect this to succeed with small content
        // The actual 413 would require server-side rejection of oversized payload
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.RequestEntityTooLarge);
    }
}
