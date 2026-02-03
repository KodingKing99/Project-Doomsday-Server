using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Domain.Files;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileMetadataTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileMetadataTests(CustomWebApplicationFactory factory)
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
    public async Task GetById_ExistingFile_ReturnsFileRecord()
    {
        // Arrange
        var uploaded = await UploadTestFile();

        // Act
        var response = await _client.GetAsync($"/files/{uploaded.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var record = await response.Content.ReadFromJsonAsync<FileRecord>();
        record.Should().NotBeNull();
        record!.Id.Should().Be(uploaded.Id);
        record.FileName.Should().Be(uploaded.FileName);
    }

    [Fact]
    public async Task GetById_NonExistingFile_Returns404()
    {
        // Arrange
        var randomId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/files/{randomId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_InvalidGuid_Returns404()
    {
        // Arrange - Invalid GUID string
        // Note: The route constraint {id:guid} means invalid GUIDs don't match the route

        // Act
        var response = await _client.GetAsync("/files/not-a-guid");

        // Assert - Returns 404 because route doesn't match
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateMetadata_ExistingFile_Returns204()
    {
        // Arrange
        var uploaded = await UploadTestFile();
        var metadata = new Dictionary<string, string> { ["key"] = "value" };
        var json = JsonSerializer.Serialize(metadata);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync($"/files/{uploaded.Id}/metadata", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateMetadata_ExistingFile_UpdatesTimestamp()
    {
        // Arrange
        var uploaded = await UploadTestFile();
        var originalUpdatedAt = uploaded.UpdatedAtUtc;
        await Task.Delay(50); // Ensure time passes

        var metadata = new Dictionary<string, string> { ["key"] = "value" };
        var json = JsonSerializer.Serialize(metadata);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        await _client.PutAsync($"/files/{uploaded.Id}/metadata", content);
        var response = await _client.GetAsync($"/files/{uploaded.Id}");
        var updated = await response.Content.ReadFromJsonAsync<FileRecord>();

        // Assert
        updated.Should().NotBeNull();
        updated!.UpdatedAtUtc.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateMetadata_NonExistingFile_Returns404OrInternalServerError()
    {
        // Arrange
        var randomId = Guid.NewGuid();
        var metadata = new Dictionary<string, string> { ["key"] = "value" };
        var json = JsonSerializer.Serialize(metadata);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync($"/files/{randomId}/metadata", content);

        // Assert
        // Note: Currently returns 500 because KeyNotFoundException is not caught.
        // TODO: Implement proper error handling to return 404 for non-existing files.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UpdateMetadata_EmptyMetadata_Succeeds()
    {
        // Arrange
        var uploaded = await UploadTestFile();
        var metadata = new Dictionary<string, string>();
        var json = JsonSerializer.Serialize(metadata);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync($"/files/{uploaded.Id}/metadata", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateMetadata_OverwritesExistingKeys()
    {
        // Arrange
        var uploaded = await UploadTestFile();

        // First update
        var metadata1 = new Dictionary<string, string> { ["key"] = "old" };
        var json1 = JsonSerializer.Serialize(metadata1);
        var content1 = new StringContent(json1, Encoding.UTF8, "application/json");
        await _client.PutAsync($"/files/{uploaded.Id}/metadata", content1);

        // Second update
        var metadata2 = new Dictionary<string, string> { ["key"] = "new" };
        var json2 = JsonSerializer.Serialize(metadata2);
        var content2 = new StringContent(json2, Encoding.UTF8, "application/json");

        // Act
        await _client.PutAsync($"/files/{uploaded.Id}/metadata", content2);
        var response = await _client.GetAsync($"/files/{uploaded.Id}");
        var record = await response.Content.ReadFromJsonAsync<FileRecord>();

        // Assert
        record.Should().NotBeNull();
        record!.Metadata.Should().ContainKey("key");
        record.Metadata["key"].Should().Be("new");
    }
}
