using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

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

    private async Task<File> CreateTestFile(string fileName = "test.txt")
    {
        var record = new File
        {
            FileName = fileName,
            ContentType = "text/plain",
            SizeBytes = 100,
        };
        var response = await _client.PostAsJsonAsync("/files", record);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        return result!.File;
    }

    [Fact]
    public async Task Delete_ExistingFile_Returns204()
    {
        // Arrange
        var created = await CreateTestFile();

        // Act
        var response = await _client.DeleteAsync($"/files/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ExistingFile_RemovesFromStorage()
    {
        // Arrange
        var created = await CreateTestFile();

        // Act
        await _client.DeleteAsync($"/files/{created.Id}");

        // Assert - storage is called with the StorageKey, not the Id
        await _factory
            .FileStorageSubstitute!.Received(1)
            .DeleteAsync(created.StorageKey!, Arg.Any<CancellationToken>());
        _factory.DeletedFileIds.Should().Contain(created.StorageKey!);
    }

    [Fact]
    public async Task Delete_ExistingFile_RemovesFromRepository()
    {
        // Arrange
        var created = await CreateTestFile();

        // Act
        await _client.DeleteAsync($"/files/{created.Id}");
        var response = await _client.GetAsync($"/files/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistingFile_Returns204()
    {
        // Idempotent delete - no error for missing file
        // Arrange
        var randomId = Guid.NewGuid().ToString("N");

        // Act
        var response = await _client.DeleteAsync($"/files/{randomId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
