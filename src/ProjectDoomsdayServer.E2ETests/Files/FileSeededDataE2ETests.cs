using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MongoDB.Bson;
using ProjectDoomsdayServer.E2ETests.Infrastructure;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.E2ETests.Files;

[Collection(E2ETestCollectionDefinition.CollectionName)]
public sealed class FileSeededDataE2ETests : IAsyncLifetime
{
    private const string SeededFileName = "seeded.txt";
    private const int SeededSizeBytes = 42;

    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;

    public FileSeededDataE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

    public Task InitializeAsync()
    {
        _factory = new E2EWebApplicationFactory(_infra);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task SeededFile_IsReturnedByGetById()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var seededFile = new File
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = userId,
            FileName = SeededFileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = SeededSizeBytes,
            StorageKey = $"{userId}/seeded-object",
        };
        await _infra.SeedFilesAsync([seededFile]);
        var client = _factory.CreateClientAs(userId);

        // Act
        var getResponse = await client.GetAsync(FileRoutes.ById(seededFile.Id!));

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<File>();
        fetched.Should().NotBeNull();
        fetched!.FileName.Should().Be(SeededFileName);
        fetched.UserId.Should().Be(userId);
        fetched.SizeBytes.Should().Be(SeededSizeBytes);
    }

    [Fact]
    public async Task SeededFiles_AreReturnedByList()
    {
        // Arrange
        var userId = E2ETestHelpers.UniqueUserId();
        var seededFiles = Enumerable
            .Range(1, 3)
            .Select(i => new File
            {
                Id = ObjectId.GenerateNewId().ToString(),
                UserId = userId,
                FileName = $"seeded-{i}.txt",
                ContentType = TestConstants.PlainTextContentType,
                SizeBytes = i * 10,
                StorageKey = $"{userId}/seeded-object-{i}",
            })
            .ToList();
        await _infra.SeedFilesAsync(seededFiles);
        var client = _factory.CreateClientAs(userId);

        // Act
        var listResponse = await client.GetAsync(FileRoutes.List(skip: 0, take: 50));

        // Assert
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var files = await listResponse.Content.ReadFromJsonAsync<List<File>>();
        files.Should().NotBeNull();
        files!.Should().HaveCount(3);
        files
            .Select(f => f.FileName)
            .Should()
            .BeEquivalentTo(["seeded-1.txt", "seeded-2.txt", "seeded-3.txt"]);
    }
}
