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
public sealed class FileOwnershipE2ETests : IAsyncLifetime
{
    private const int DefaultSizeBytes = 100;

    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;

    public FileOwnershipE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

    public Task InitializeAsync()
    {
        _factory = new E2EWebApplicationFactory(_infra);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static async Task<CreateFileResult> CreateFileAsUser(HttpClient client)
    {
        var input = new CreateFileInput
        {
            FileName = "ownership-test.txt",
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = DefaultSizeBytes,
        };
        var response = await client.PostAsJsonAsync(FileRoutes.Base, input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateFileResult>())!;
    }

    [Fact]
    public async Task CrossUser_Get_Returns404ForNonOwner_And200ForOwner()
    {
        // Arrange
        var userAId = E2ETestHelpers.UniqueUserId();
        var userBId = E2ETestHelpers.UniqueUserId();
        var userAClient = _factory.CreateClientAs(userAId);
        var userBClient = _factory.CreateClientAs(userBId);
        var createResult = await CreateFileAsUser(userAClient);
        var fileId = createResult.File.Id!;

        // Act
        var nonOwnerResponse = await userBClient.GetAsync(FileRoutes.ById(fileId));
        var ownerResponse = await userAClient.GetAsync(FileRoutes.ById(fileId));

        // Assert
        nonOwnerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ownerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CrossUser_Download_Returns404ForNonOwner()
    {
        // Arrange
        var userAId = E2ETestHelpers.UniqueUserId();
        var userBId = E2ETestHelpers.UniqueUserId();
        var userAClient = _factory.CreateClientAs(userAId);
        var userBClient = _factory.CreateClientAs(userBId);
        var createResult = await CreateFileAsUser(userAClient);
        var fileId = createResult.File.Id!;

        // Act
        var nonOwnerResponse = await userBClient.GetAsync(FileRoutes.Content(fileId));

        // Assert
        nonOwnerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossUser_Update_Returns404ForNonOwner()
    {
        // Arrange
        var userAId = E2ETestHelpers.UniqueUserId();
        var userBId = E2ETestHelpers.UniqueUserId();
        var userAClient = _factory.CreateClientAs(userAId);
        var userBClient = _factory.CreateClientAs(userBId);
        var createResult = await CreateFileAsUser(userAClient);
        var fileId = createResult.File.Id!;
        var updateRecord = new File
        {
            FileName = "hijacked.txt",
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 200,
        };

        // Act
        var nonOwnerResponse = await userBClient.PutAsJsonAsync(
            FileRoutes.ById(fileId),
            updateRecord
        );

        // Assert
        nonOwnerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossUser_Delete_Returns404ForNonOwner()
    {
        // Arrange
        var userAId = E2ETestHelpers.UniqueUserId();
        var userBId = E2ETestHelpers.UniqueUserId();
        var userAClient = _factory.CreateClientAs(userAId);
        var userBClient = _factory.CreateClientAs(userBId);
        var createResult = await CreateFileAsUser(userAClient);
        var fileId = createResult.File.Id!;

        // Act
        var nonOwnerResponse = await userBClient.DeleteAsync(FileRoutes.ById(fileId));

        // Assert
        nonOwnerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
