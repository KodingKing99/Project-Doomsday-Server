using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files;
using Xunit;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FilesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FilesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_ReturnsUploadUrl()
    {
        // Arrange
        var record = new File
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            SizeBytes = 1024,
            UserId = "user123",
        };

        // Act
        var response = await _client.PostAsJsonAsync("/files", record);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        result.Should().NotBeNull();
        result!.File.Should().NotBeNull();
        result.File.Id.Should().NotBeNullOrEmpty();
        result.UploadUrl.Should().NotBeNullOrWhiteSpace();
        result.UploadUrl.Should().StartWith("https://");
    }

    [Fact]
    public async Task Create_SetsStorageKey()
    {
        // Arrange
        var record = new File
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            SizeBytes = 1024,
            UserId = "user123",
        };

        // Act
        var response = await _client.PostAsJsonAsync("/files", record);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        result.Should().NotBeNull();
        result!.File.StorageKey.Should().Be($"user123/{result.File.Id}");
    }

    [Fact]
    public async Task Create_PresignedUrlUsesStorageKey()
    {
        // Arrange
        var record = new File
        {
            FileName = "document.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
            UserId = "user456",
        };

        // Act
        var response = await _client.PostAsJsonAsync("/files", record);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();

        // Assert - Verify presigned URL was requested with the storage key
        await _factory
            .FileStorageSubstitute!.Received(1)
            .GetPresignedUploadUrlAsync(result!.File.StorageKey!, Arg.Any<CancellationToken>());
    }
}
