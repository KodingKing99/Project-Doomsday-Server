using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files;
using Xunit;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FilesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FilesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPresignedUploadUrl_ReturnsUrl()
    {
        // Arrange
        var fileName = "test.txt";

        // Act
        var response = await _client.GetAsync($"/files/presigned-upload-url?fileName={System.Uri.EscapeDataString(fileName)}");

        // Assert
        response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync();
    body.Should().NotBeNullOrWhiteSpace();
    body.Should().StartWith("https://");

    // Verify the substitute was called
    var sub = _factory.FileStorageSubstitute as IFileStorage;
    Assert.NotNull(sub);
    // NSubstitute verification: ensure the method was called at least once with the filename
    ((IFileStorage)sub!).Received(1).GetPresignedUploadUrlAsync(fileName, Arg.Any<System.Threading.CancellationToken>());
    }
}
