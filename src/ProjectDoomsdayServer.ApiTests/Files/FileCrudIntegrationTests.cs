using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.DbModels;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileCrudIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string IntegrationTestFileName = "integration-test.txt";
    private const string UpdatedFileName = "integration-test-updated.txt";
    private const string EnvironmentMetadataKey = "environment";
    private const string EnvironmentMetadataValue = "test";
    private const string VersionMetadataKey = "version";
    private const string VersionMetadataValue = "1.0";

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
        // Arrange
        var record = new CreateFileInput
        {
            FileName = IntegrationTestFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 1024,
        };

        // Act & Assert - create file record and get ID
        var createResponse = await _client.PostAsJsonAsync(FileRoutes.Base, record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult.Should().NotBeNull();
        var createdRecord = createResult!.File;
        createdRecord.Id.Should().NotBeNullOrEmpty();
        createResult.UploadUrl.Should().NotBeNullOrWhiteSpace();
        var fileId = createdRecord.Id!;

        // Act & Assert - list files and verify file appears
        var listResponse = await _client.GetAsync(FileRoutes.List());
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var files = await listResponse.Content.ReadFromJsonAsync<List<File>>();
        files.Should().NotBeNull();
        files.Should().Contain(f => f.Id == fileId);

        // Act & Assert - get file by ID and verify metadata
        var getResponse = await _client.GetAsync(FileRoutes.ById(fileId));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedRecord = await getResponse.Content.ReadFromJsonAsync<File>();
        fetchedRecord.Should().NotBeNull();
        fetchedRecord!.FileName.Should().Be(IntegrationTestFileName);
        fetchedRecord.ContentType.Should().Be(TestConstants.PlainTextContentType);

        // Act & Assert - update via PUT and verify change
        var updatedRecord = new File
        {
            FileName = UpdatedFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 2048,
            Metadata =
            {
                [EnvironmentMetadataKey] = EnvironmentMetadataValue,
                [VersionMetadataKey] = VersionMetadataValue,
            },
        };
        var updateResponse = await _client.PutAsJsonAsync(FileRoutes.ById(fileId), updatedRecord);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getAfterUpdateResponse = await _client.GetAsync(FileRoutes.ById(fileId));
        var afterUpdate = await getAfterUpdateResponse.Content.ReadFromJsonAsync<File>();
        afterUpdate.Should().NotBeNull();
        afterUpdate!.FileName.Should().Be(UpdatedFileName);
        afterUpdate.SizeBytes.Should().Be(2048);
        afterUpdate.Metadata.Should().ContainKey(EnvironmentMetadataKey);
        afterUpdate.Metadata[EnvironmentMetadataKey].Should().Be(EnvironmentMetadataValue);
        afterUpdate.Metadata[VersionMetadataKey].Should().Be(VersionMetadataValue);

        // Act & Assert - delete file and verify 204
        var deleteResponse = await _client.DeleteAsync(FileRoutes.ById(fileId));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act & Assert - get file by ID and verify 404
        var getAfterDeleteResponse = await _client.GetAsync(FileRoutes.ById(fileId));
        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateThenDownload_Succeeds()
    {
        // Arrange - client would upload to S3 separately
        var record = new CreateFileInput
        {
            FileName = "binary-test.bin",
            ContentType = TestConstants.OctetStreamContentType,
            SizeBytes = 1024,
        };

        // Act - create file record
        var createResponse = await _client.PostAsJsonAsync(FileRoutes.Base, record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult.Should().NotBeNull();
        var downloadResponse = await _client.GetAsync(FileRoutes.Content(createResult!.File.Id!));

        // Assert - content comes from mocked storage
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
