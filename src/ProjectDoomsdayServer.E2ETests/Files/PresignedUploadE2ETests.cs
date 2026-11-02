using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.E2ETests.Infrastructure;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.E2ETests.Files;

[Collection(E2ETestCollectionDefinition.CollectionName)]
public sealed class PresignedUploadE2ETests : IAsyncLifetime
{
    private const int LargeBinarySizeBytes = 512 * 1024;
    private const string EnvironmentMetadataKey = "env";
    private const string EnvironmentMetadataValue = "e2e";
    private const string VersionMetadataKey = "version";
    private const string VersionMetadataValue = "42";

    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;
    private readonly HttpClient _rawHttpClient = new();

    public PresignedUploadE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

    public Task InitializeAsync()
    {
        _factory = new E2EWebApplicationFactory(_infra);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _rawHttpClient.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TextContent_RoundTrip_BytesMatchAfterDownload()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        var content = E2ETestHelpers.Utf8Bytes("Hello, E2E world!");
        var record = new CreateFileInput
        {
            FileName = "hello.txt",
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = content.Length,
        };

        // Act & Assert - create, PUT bytes directly to the presigned MinIO URL, then download
        var createResponse = await client.PostAsJsonAsync(FileRoutes.Base, record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        createResult.Should().NotBeNull();
        var uploadUrl = createResult!.UploadUrl;
        var fileId = createResult.File.Id!;
        using var putContent = new ByteArrayContent(content);
        var putResponse = await _rawHttpClient.PutAsync(uploadUrl, putContent);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadResponse = await client.GetAsync(FileRoutes.Content(fileId));
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(content);
    }

    [Fact]
    public async Task LargeBinary_512KB_RoundTrip_BytesMatchAfterDownload()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        var content = E2ETestHelpers.RandomBytes(LargeBinarySizeBytes);
        var record = new CreateFileInput
        {
            FileName = "binary.bin",
            ContentType = TestConstants.OctetStreamContentType,
            SizeBytes = content.Length,
        };

        // Act & Assert - create, upload, then download
        var createResponse = await client.PostAsJsonAsync(FileRoutes.Base, record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var uploadUrl = createResult!.UploadUrl;
        var fileId = createResult.File.Id!;
        using var putContent = new ByteArrayContent(content);
        var putResponse = await _rawHttpClient.PutAsync(uploadUrl, putContent);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadResponse = await client.GetAsync(FileRoutes.Content(fileId));
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(content);
    }

    [Fact]
    public async Task Metadata_PersistedToMongoDB_ReturnedOnGet()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var client = _factory.CreateClientAs(userId);
        var record = new CreateFileInput
        {
            FileName = "meta.txt",
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 10,
            Metadata =
            {
                [EnvironmentMetadataKey] = EnvironmentMetadataValue,
                [VersionMetadataKey] = VersionMetadataValue,
            },
        };

        // Act & Assert - create, then get
        var createResponse = await client.PostAsJsonAsync(FileRoutes.Base, record);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateFileResult>();
        var fileId = createResult!.File.Id!;
        var getResponse = await client.GetAsync(FileRoutes.ById(fileId));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<File>();
        fetched.Should().NotBeNull();
        fetched!
            .Metadata.Should()
            .ContainKey(EnvironmentMetadataKey)
            .WhoseValue.Should()
            .Be(EnvironmentMetadataValue);
        fetched
            .Metadata.Should()
            .ContainKey(VersionMetadataKey)
            .WhoseValue.Should()
            .Be(VersionMetadataValue);
    }
}
