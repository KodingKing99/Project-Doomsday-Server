using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

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

    private async Task<File> CreateTestFile(File? record = null)
    {
        record ??= new File
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            SizeBytes = 100,
        };
        var response = await _client.PostAsJsonAsync("/files", record);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        return result!.File;
    }

    [Fact]
    public async Task Create_NewFile_ReturnsCreatedWithFile()
    {
        // Arrange
        var record = new File
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            SizeBytes = 1024,
        };

        // Act
        var response = await _client.PostAsJsonAsync("/files", record);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult.Should().NotBeNull();
        var result = createResult!.File;
        result.Id.Should().NotBeNullOrEmpty();
        result.FileName.Should().Be("test.txt");
        result.ContentType.Should().Be("text/plain");
        result.SizeBytes.Should().Be(1024);
        createResult.UploadUrl.Should().NotBeNullOrWhiteSpace();
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_ExistingFile_ReturnsOkWithUpdatedRecord()
    {
        // Arrange
        var original = await CreateTestFile();
        var updated = new File
        {
            FileName = "updated.txt",
            ContentType = "text/plain",
            SizeBytes = 2048,
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/files/{original.Id}", updated);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<File>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(original.Id);
        result.FileName.Should().Be("updated.txt");
        result.SizeBytes.Should().Be(2048);
    }

    [Fact]
    public async Task Create_WithMetadata_PersistsMetadata()
    {
        // Arrange
        var record = new File
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            SizeBytes = 100,
            Metadata = { ["key1"] = "value1", ["key2"] = "value2" },
        };

        // Act
        var response = await _client.PostAsJsonAsync("/files", record);
        var createResult = await response.Content.ReadFromJsonAsync<CreateFileResult>();

        // Assert
        createResult.Should().NotBeNull();
        var result = createResult!.File;
        result.Metadata.Should().ContainKey("key1");
        result.Metadata["key1"].Should().Be("value1");
        result.Metadata.Should().ContainKey("key2");
        result.Metadata["key2"].Should().Be("value2");
    }

    [Fact]
    public async Task Update_UpdatesTimestamp()
    {
        // Arrange
        var original = await CreateTestFile();
        var originalUpdatedAt = original.UpdatedAtUtc;
        await Task.Delay(50);

        var updated = new File
        {
            FileName = "updated.txt",
            ContentType = "text/plain",
            SizeBytes = 100,
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/files/{original.Id}", updated);
        var result = await response.Content.ReadFromJsonAsync<File>();

        // Assert
        result.Should().NotBeNull();
        result!.UpdatedAtUtc.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task GetById_ExistingFile_ReturnsFile()
    {
        // Arrange
        var created = await CreateTestFile();

        // Act
        var response = await _client.GetAsync($"/files/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var record = await response.Content.ReadFromJsonAsync<File>();
        record.Should().NotBeNull();
        record!.Id.Should().Be(created.Id);
        record.FileName.Should().Be(created.FileName);
    }

    [Fact]
    public async Task GetById_NonExistingFile_Returns404()
    {
        // Arrange
        var randomId = Guid.NewGuid().ToString("N");

        // Act
        var response = await _client.GetAsync($"/files/{randomId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_InvalidId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/files/not-a-valid-id");

        // Assert - Returns 404 because no file exists with that ID
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_ReturnsFiles()
    {
        // Arrange
        await CreateTestFile(
            new File
            {
                FileName = "file1.txt",
                ContentType = "text/plain",
                SizeBytes = 100,
            }
        );
        await CreateTestFile(
            new File
            {
                FileName = "file2.txt",
                ContentType = "text/plain",
                SizeBytes = 200,
            }
        );

        // Act
        var response = await _client.GetAsync("/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records!.Count.Should().BeGreaterThanOrEqualTo(2);
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

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/files/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
