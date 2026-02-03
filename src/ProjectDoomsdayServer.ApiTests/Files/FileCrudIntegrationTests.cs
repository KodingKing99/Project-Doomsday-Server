using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Domain.Files;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileCrudIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileCrudIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullCrudFlow_UploadListGetUpdateDelete_Succeeds()
    {
        // 1. Upload file -> get ID
        var uploadContent = TestHelpers.CreateTextContent("Integration test file content");
        using var uploadForm = TestHelpers.CreateFileUpload(
            "integration-test.txt",
            uploadContent,
            "text/plain"
        );
        var uploadResponse = await _client.PostAsync("/files", uploadForm);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploadedRecord = await uploadResponse.Content.ReadFromJsonAsync<FileRecord>();
        uploadedRecord.Should().NotBeNull();
        var fileId = uploadedRecord!.Id;

        // 2. List files -> verify file appears
        var listResponse = await _client.GetAsync("/files");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var files = await listResponse.Content.ReadFromJsonAsync<List<FileRecord>>();
        files.Should().NotBeNull();
        files.Should().Contain(f => f.Id == fileId);

        // 3. Get file by ID -> verify metadata
        var getResponse = await _client.GetAsync($"/files/{fileId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedRecord = await getResponse.Content.ReadFromJsonAsync<FileRecord>();
        fetchedRecord.Should().NotBeNull();
        fetchedRecord!.FileName.Should().Be("integration-test.txt");
        fetchedRecord.ContentType.Should().Be("text/plain");

        // 4. Update metadata -> verify change
        var metadata = new Dictionary<string, string> { ["environment"] = "test", ["version"] = "1.0" };
        var metadataJson = JsonSerializer.Serialize(metadata);
        var metadataContent = new StringContent(metadataJson, Encoding.UTF8, "application/json");
        var updateResponse = await _client.PutAsync($"/files/{fileId}/metadata", metadataContent);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify metadata was updated
        var getAfterUpdateResponse = await _client.GetAsync($"/files/{fileId}");
        var updatedRecord = await getAfterUpdateResponse.Content.ReadFromJsonAsync<FileRecord>();
        updatedRecord.Should().NotBeNull();
        updatedRecord!.Metadata.Should().ContainKey("environment");
        updatedRecord.Metadata["environment"].Should().Be("test");
        updatedRecord.Metadata["version"].Should().Be("1.0");

        // 5. Delete file -> verify 204
        var deleteResponse = await _client.DeleteAsync($"/files/{fileId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Get file by ID -> verify 404
        var getAfterDeleteResponse = await _client.GetAsync($"/files/{fileId}");
        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadThenDownload_ContentMatches()
    {
        // Arrange - Create test content
        var originalContent = TestHelpers.GenerateRandomBytes(1024);
        using var uploadForm = TestHelpers.CreateFileUpload("binary-test.bin", originalContent, "application/octet-stream");

        // Act - Upload file
        var uploadResponse = await _client.PostAsync("/files", uploadForm);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var record = await uploadResponse.Content.ReadFromJsonAsync<FileRecord>();
        record.Should().NotBeNull();

        // Download file
        var downloadResponse = await _client.GetAsync($"/files/{record!.Id}/content");
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadedContent = await downloadResponse.Content.ReadAsByteArrayAsync();

        // Assert - Content matches byte-for-byte
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }
}
