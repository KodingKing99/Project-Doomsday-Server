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

public class FileMetadataTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string InvalidFileId = "not-a-valid-id";
    private const string UpdatedFileName = "updated.txt";
    private const string FirstMetadataKey = "key1";
    private const string FirstMetadataValue = "value1";
    private const string SecondMetadataKey = "key2";
    private const string SecondMetadataValue = "value2";

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
            FileName = TestConstants.DefaultTestFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 100,
        };
        var response = await _client.PostAsJsonAsync(FileRoutes.Base, record);
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
            FileName = TestConstants.DefaultTestFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 1024,
        };

        // Act
        var response = await _client.PostAsJsonAsync(FileRoutes.Base, record);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult.Should().NotBeNull();
        var result = createResult!.File;
        result.Id.Should().NotBeNullOrEmpty();
        result.FileName.Should().Be(TestConstants.DefaultTestFileName);
        result.ContentType.Should().Be(TestConstants.PlainTextContentType);
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
            FileName = UpdatedFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 2048,
        };

        // Act
        var response = await _client.PutAsJsonAsync(FileRoutes.ById(original.Id!), updated);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<File>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(original.Id);
        result.FileName.Should().Be(UpdatedFileName);
        result.SizeBytes.Should().Be(2048);
    }

    [Fact]
    public async Task Create_WithMetadata_PersistsMetadata()
    {
        // Arrange
        var record = new File
        {
            FileName = TestConstants.DefaultTestFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 100,
            Metadata =
            {
                [FirstMetadataKey] = FirstMetadataValue,
                [SecondMetadataKey] = SecondMetadataValue,
            },
        };

        // Act
        var response = await _client.PostAsJsonAsync(FileRoutes.Base, record);
        var createResult = await response.Content.ReadFromJsonAsync<CreateFileResult>();

        // Assert
        createResult.Should().NotBeNull();
        var result = createResult!.File;
        result.Metadata.Should().ContainKey(FirstMetadataKey);
        result.Metadata[FirstMetadataKey].Should().Be(FirstMetadataValue);
        result.Metadata.Should().ContainKey(SecondMetadataKey);
        result.Metadata[SecondMetadataKey].Should().Be(SecondMetadataValue);
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
            FileName = UpdatedFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 100,
        };

        // Act
        var response = await _client.PutAsJsonAsync(FileRoutes.ById(original.Id!), updated);
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
        var response = await _client.GetAsync(FileRoutes.ById(created.Id!));

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
        var response = await _client.GetAsync(FileRoutes.ById(randomId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_InvalidId_Returns404()
    {
        // Act
        var response = await _client.GetAsync(FileRoutes.ById(InvalidFileId));

        // Assert
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
                ContentType = TestConstants.PlainTextContentType,
                SizeBytes = 100,
            }
        );
        await CreateTestFile(
            new File
            {
                FileName = "file2.txt",
                ContentType = TestConstants.PlainTextContentType,
                SizeBytes = 200,
            }
        );

        // Act
        var response = await _client.GetAsync(FileRoutes.List());

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
        var response = await _client.DeleteAsync(FileRoutes.ById(created.Id!));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act & Assert - verify it's gone
        var getResponse = await _client.GetAsync(FileRoutes.ById(created.Id!));
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
