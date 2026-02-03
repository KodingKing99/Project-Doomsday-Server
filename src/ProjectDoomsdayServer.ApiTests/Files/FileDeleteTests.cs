using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.Files;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileDeleteTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileDeleteTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _client = factory.CreateClient();
    }

    private async Task<FileRecord> UploadTestFile(string fileName = "test.txt")
    {
        var content = TestHelpers.CreateTextContent("Test content");
        using var form = TestHelpers.CreateFileUpload(fileName, content, "text/plain");
        var response = await _client.PostAsync("/files", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileRecord>())!;
    }

    [Fact]
    public async Task Delete_ExistingFile_Returns204()
    {
        // Arrange
        var uploaded = await UploadTestFile();

        // Act
        var response = await _client.DeleteAsync($"/files/{uploaded.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ExistingFile_RemovesFromStorage()
    {
        // Arrange
        var uploaded = await UploadTestFile();

        // Act
        await _client.DeleteAsync($"/files/{uploaded.Id}");

        // Assert
        await _factory.FileStorageSubstitute!
            .Received(1)
            .DeleteAsync(uploaded.Id, Arg.Any<CancellationToken>());
        _factory.DeletedFileIds.Should().Contain(uploaded.Id);
    }

    [Fact]
    public async Task Delete_ExistingFile_RemovesFromRepository()
    {
        // Arrange
        var uploaded = await UploadTestFile();

        // Act
        await _client.DeleteAsync($"/files/{uploaded.Id}");
        var response = await _client.GetAsync($"/files/{uploaded.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistingFile_Returns204()
    {
        // Idempotent delete - no error for missing file
        // Arrange
        var randomId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/files/{randomId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
