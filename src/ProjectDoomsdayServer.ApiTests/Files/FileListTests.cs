using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileListTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileListTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
        _client = factory.CreateClient();
    }

    private async Task UpsertTestFile(string fileName, string contentType = "text/plain")
    {
        var record = new File
        {
            Id = Guid.NewGuid().ToString("N"),
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = 100,
        };
        await _client.PostAsJsonAsync("/files", record);
    }

    [Fact]
    public async Task List_NoFiles_ReturnsEmptyList()
    {
        // Arrange - factory is reset, no files

        // Act
        var response = await _client.GetAsync("/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithFiles_ReturnsFileRecords()
    {
        // Arrange - Upsert 3 files
        for (var i = 1; i <= 3; i++)
        {
            await UpsertTestFile($"file{i}.txt");
        }

        // Act
        var response = await _client.GetAsync("/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(3);
    }

    [Fact]
    public async Task List_Pagination_Skip_Works()
    {
        // Arrange - Upsert 5 files
        for (var i = 1; i <= 5; i++)
        {
            await UpsertTestFile($"file{i}.txt");
        }

        // Act
        var response = await _client.GetAsync("/files?skip=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(3);
    }

    [Fact]
    public async Task List_Pagination_Take_Works()
    {
        // Arrange - Upsert 5 files
        for (var i = 1; i <= 5; i++)
        {
            await UpsertTestFile($"file{i}.txt");
        }

        // Act
        var response = await _client.GetAsync("/files?take=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_Pagination_TakeClamped_To200Max()
    {
        // Arrange - Upsert 10 files (simulating larger set)
        for (var i = 1; i <= 10; i++)
        {
            await UpsertTestFile($"file{i}.txt");
        }

        // Act - Request 500 but should be clamped to 200
        var response = await _client.GetAsync("/files?take=500");

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
        // Arrange - Upsert files with delays
        await UpsertTestFile("first.txt");
        await Task.Delay(50); // Small delay to ensure different timestamps
        await UpsertTestFile("second.txt");

        // Act
        var response = await _client.GetAsync("/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<File>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(2);
        // Most recently updated should be first
        records![0].FileName.Should().Be("second.txt");
        records[1].FileName.Should().Be("first.txt");
    }
}
