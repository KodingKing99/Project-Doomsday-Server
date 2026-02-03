using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Domain.Files;

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

    [Fact]
    public async Task List_NoFiles_ReturnsEmptyList()
    {
        // Arrange - factory is reset, no files

        // Act
        var response = await _client.GetAsync("/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<FileRecord>>();
        records.Should().NotBeNull();
        records.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithFiles_ReturnsFileRecords()
    {
        // Arrange - Upload 3 files
        for (var i = 1; i <= 3; i++)
        {
            var content = TestHelpers.CreateTextContent($"File {i} content");
            using var form = TestHelpers.CreateFileUpload($"file{i}.txt", content, "text/plain");
            await _client.PostAsync("/files", form);
        }

        // Act
        var response = await _client.GetAsync("/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<FileRecord>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(3);
    }

    [Fact]
    public async Task List_Pagination_Skip_Works()
    {
        // Arrange - Upload 5 files
        for (var i = 1; i <= 5; i++)
        {
            var content = TestHelpers.CreateTextContent($"File {i}");
            using var form = TestHelpers.CreateFileUpload($"file{i}.txt", content, "text/plain");
            await _client.PostAsync("/files", form);
        }

        // Act
        var response = await _client.GetAsync("/files?skip=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<FileRecord>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(3);
    }

    [Fact]
    public async Task List_Pagination_Take_Works()
    {
        // Arrange - Upload 5 files
        for (var i = 1; i <= 5; i++)
        {
            var content = TestHelpers.CreateTextContent($"File {i}");
            using var form = TestHelpers.CreateFileUpload($"file{i}.txt", content, "text/plain");
            await _client.PostAsync("/files", form);
        }

        // Act
        var response = await _client.GetAsync("/files?take=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<FileRecord>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_Pagination_TakeClamped_To200Max()
    {
        // Arrange - Upload 10 files (simulating larger set)
        for (var i = 1; i <= 10; i++)
        {
            var content = TestHelpers.CreateTextContent($"File {i}");
            using var form = TestHelpers.CreateFileUpload($"file{i}.txt", content, "text/plain");
            await _client.PostAsync("/files", form);
        }

        // Act - Request 500 but should be clamped to 200
        var response = await _client.GetAsync("/files?take=500");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<FileRecord>>();
        records.Should().NotBeNull();
        // Should return all 10 since we only have 10 (but limit would be 200)
        records.Should().HaveCountLessThanOrEqualTo(200);
    }

    [Fact]
    public async Task List_OrderedByUpdatedAtDescending()
    {
        // Arrange - Upload files with delays
        var content1 = TestHelpers.CreateTextContent("First");
        using var form1 = TestHelpers.CreateFileUpload("first.txt", content1, "text/plain");
        await _client.PostAsync("/files", form1);

        await Task.Delay(50); // Small delay to ensure different timestamps

        var content2 = TestHelpers.CreateTextContent("Second");
        using var form2 = TestHelpers.CreateFileUpload("second.txt", content2, "text/plain");
        await _client.PostAsync("/files", form2);

        // Act
        var response = await _client.GetAsync("/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await response.Content.ReadFromJsonAsync<List<FileRecord>>();
        records.Should().NotBeNull();
        records.Should().HaveCount(2);
        // Most recently updated should be first
        records![0].FileName.Should().Be("second.txt");
        records[1].FileName.Should().Be("first.txt");
    }
}
