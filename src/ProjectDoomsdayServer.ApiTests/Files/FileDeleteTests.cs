using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using ProjectDoomsdayServer.ApiTests.TestSupport;
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

    private async Task<FileRecord> UpsertTestFile(string fileName = "test.txt")
    {
        var record = new FileRecord
        {
            FileName = fileName,
            ContentType = "text/plain",
            SizeBytes = 100,
        };
        var response = await _client.PostAsJsonAsync("/files", record);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileRecord>())!;
    }

    [Fact]
    public async Task Delete_ExistingFile_Returns204()
    {
        // Arrange
        var created = await UpsertTestFile();

        // Act
        var response = await _client.DeleteAsync($"/files/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ExistingFile_RemovesFromStorage()
    {
        // Arrange
        var created = await UpsertTestFile();

        // Act
        await _client.DeleteAsync($"/files/{created.Id}");

        // Assert
        await _factory
            .FileStorageSubstitute!.Received(1)
            .DeleteAsync(created.Id, Arg.Any<CancellationToken>());
        _factory.DeletedFileIds.Should().Contain(created.Id);
    }

    [Fact]
    public async Task Delete_ExistingFile_RemovesFromRepository()
    {
        // Arrange
        var created = await UpsertTestFile();

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
        var randomId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/files/{randomId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
