using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.TestSupport;
using Xunit;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string FirstUserId = "user123";
    private const string SecondUserId = "user456";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_ReturnsUploadUrl()
    {
        // Arrange
        var record = new CreateFileInput
        {
            FileName = TestConstants.DefaultTestFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 1024,
        };

        // Act
        var response = await _client.PostAsJsonAsync(FileRoutes.Base, record);

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
        var record = new CreateFileInput
        {
            FileName = TestConstants.DefaultTestFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 1024,
        };
        var client = _factory.CreateClientAs(FirstUserId);

        // Act
        var response = await client.PostAsJsonAsync(FileRoutes.Base, record);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        result.Should().NotBeNull();
        result!.File.StorageKey.Should().Be($"{FirstUserId}/{result.File.Id}");
    }

    [Fact]
    public async Task Create_PresignedUrlUsesStorageKey()
    {
        // Arrange
        var record = new CreateFileInput
        {
            FileName = "document.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
        };
        var client = _factory.CreateClientAs(SecondUserId);

        // Act
        var response = await client.PostAsJsonAsync(FileRoutes.Base, record);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();

        // Assert - Verify presigned URL was requested with the storage key
        await _factory
            .FileStorageSubstitute!.Received(1)
            .GetPresignedUploadUrlAsync(result!.File.StorageKey!, Arg.Any<CancellationToken>());
    }
}
