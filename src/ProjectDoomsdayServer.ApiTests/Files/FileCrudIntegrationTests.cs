using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

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
    public async Task FullCrudFlow_CreateListGetUpdateDelete_Succeeds()
    {
        // 1. Create file record -> get ID
        var record = new File
        {
            FileName = "integration-test.txt",
            ContentType = "text/plain",
            SizeBytes = 1024,
        };
        var createResponse = await _client.PostAsJsonAsync("/files", record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult.Should().NotBeNull();
        var createdRecord = createResult!.File;
        createdRecord.Id.Should().NotBeNullOrEmpty();
        createResult.UploadUrl.Should().NotBeNullOrWhiteSpace();
        var fileId = createdRecord.Id;

        // 2. List files -> verify file appears
        var listResponse = await _client.GetAsync("/files");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var files = await listResponse.Content.ReadFromJsonAsync<List<File>>();
        files.Should().NotBeNull();
        files.Should().Contain(f => f.Id == fileId);

        // 3. Get file by ID -> verify metadata
        var getResponse = await _client.GetAsync($"/files/{fileId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedRecord = await getResponse.Content.ReadFromJsonAsync<File>();
        fetchedRecord.Should().NotBeNull();
        fetchedRecord!.FileName.Should().Be("integration-test.txt");
        fetchedRecord.ContentType.Should().Be("text/plain");

        // 4. Update via PUT -> verify change
        var updatedRecord = new File
        {
            FileName = "integration-test-updated.txt",
            ContentType = "text/plain",
            SizeBytes = 2048,
            Metadata = { ["environment"] = "test", ["version"] = "1.0" },
        };
        var updateResponse = await _client.PutAsJsonAsync($"/files/{fileId}", updatedRecord);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify update was applied
        var getAfterUpdateResponse = await _client.GetAsync($"/files/{fileId}");
        var afterUpdate = await getAfterUpdateResponse.Content.ReadFromJsonAsync<File>();
        afterUpdate.Should().NotBeNull();
        afterUpdate!.FileName.Should().Be("integration-test-updated.txt");
        afterUpdate.SizeBytes.Should().Be(2048);
        afterUpdate.Metadata.Should().ContainKey("environment");
        afterUpdate.Metadata["environment"].Should().Be("test");
        afterUpdate.Metadata["version"].Should().Be("1.0");

        // 5. Delete file -> verify 204
        var deleteResponse = await _client.DeleteAsync($"/files/{fileId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Get file by ID -> verify 404
        var getAfterDeleteResponse = await _client.GetAsync($"/files/{fileId}");
        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateThenDownload_Succeeds()
    {
        // Arrange - Create file record (client would upload to S3 separately)
        var record = new File
        {
            FileName = "binary-test.bin",
            ContentType = "application/octet-stream",
            SizeBytes = 1024,
        };

        // Act - Create file record
        var createResponse = await _client.PostAsJsonAsync("/files", record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult.Should().NotBeNull();

        // Download file (content comes from mocked storage)
        var downloadResponse = await _client.GetAsync($"/files/{createResult!.File.Id}/content");
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
