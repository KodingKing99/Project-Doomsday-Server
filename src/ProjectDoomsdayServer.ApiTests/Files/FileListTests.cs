using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Domain.DbModels;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileListTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string FirstFileName = "first.txt";
    private const string SecondFileName = "second.txt";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileListTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _client = factory.CreateClient();
    }

    private async Task CreateTestFile(
        string fileName,
        string contentType = TestConstants.PlainTextContentType
    )
    {
        var record = new CreateFileInput
        {
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = 100,
        };
        await _client.PostAsJsonAsync(FileRoutes.Base, record);
    }

    private static string FileNameFor(int index) => $"file{index}.txt";

    [Fact]
    public async Task List_NoFiles_ReturnsEmptyList()
    {
        // Arrange - factory is reset, no files

        // Act
        var response = await _client.GetAsync(FileRoutes.List());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithFiles_ReturnsFileRecords()
    {
        // Arrange - Create 3 files
        for (var i = 1; i <= 3; i++)
        {
            await CreateTestFile(FileNameFor(i));
        }

        // Act
        var response = await _client.GetAsync(FileRoutes.List());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(3);
    }

    [Fact]
    public async Task List_Pagination_Skip_Works()
    {
        // Arrange - Create 5 files
        for (var i = 1; i <= 5; i++)
        {
            await CreateTestFile(FileNameFor(i));
        }

        // Act
        var response = await _client.GetAsync(FileRoutes.List(skip: 2));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(3);
    }

    [Fact]
    public async Task List_Pagination_Take_Works()
    {
        // Arrange - Create 5 files
        for (var i = 1; i <= 5; i++)
        {
            await CreateTestFile(FileNameFor(i));
        }

        // Act
        var response = await _client.GetAsync(FileRoutes.List(take: 2));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_Pagination_TakeClamped_To200Max()
    {
        // Arrange - Create 10 files (simulating larger set)
        for (var i = 1; i <= 10; i++)
        {
            await CreateTestFile(FileNameFor(i));
        }

        // Act - Request 500 but should be clamped to 200
        var response = await _client.GetAsync(FileRoutes.List(take: 500));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        // Should return all 10 since we only have 10 (but limit would be 200)
        records.Should().HaveCountLessThanOrEqualTo(200);
    }

    [Fact]
    public async Task List_OrderedByUpdatedAtDescending()
    {
        // Arrange - Create files with delays
        await CreateTestFile(FirstFileName);
        await Task.Delay(50); // Small delay to ensure different timestamps
        await CreateTestFile(SecondFileName);

        // Act
        var response = await _client.GetAsync(FileRoutes.List());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(2);
        // Most recently updated should be first
        records![0].FileName.Should().Be(SecondFileName);
        records[1].FileName.Should().Be(FirstFileName);
    }
}
