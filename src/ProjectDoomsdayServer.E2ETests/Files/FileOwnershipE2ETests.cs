using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.E2ETests.Infrastructure;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.E2ETests.Files;

[Collection(E2ETestCollection.CollectionName)]
public sealed class FileOwnershipE2ETests : IAsyncLifetime
{
    private readonly E2EInfrastructureFixture _infra;
    private E2EWebApplicationFactory _factory = default!;

    public FileOwnershipE2ETests(E2EInfrastructureFixture infra) => _infra = infra;

    public Task InitializeAsync()
    {
        _factory = new E2EWebApplicationFactory(_infra);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<CreateFileResult> CreateFileAsUser(HttpClient client)
    {
        var input = new CreateFileInput
        {
            FileName = "ownership-test.txt",
            ContentType = "text/plain",
            SizeBytes = 100,
        };
        var response = await client.PostAsJsonAsync("/files", input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateFileResult>())!;
    }

    [Fact]
    public async Task CrossUser_Get_Returns404ForNonOwner_And200ForOwner()
    {
        var userAId = E2ETestHelpers.UniqueUserId();
        var userBId = E2ETestHelpers.UniqueUserId();
        var userAClient = _factory.CreateClientAs(userAId);
        var userBClient = _factory.CreateClientAs(userBId);

        var createResult = await CreateFileAsUser(userAClient);
        var fileId = createResult.File.Id;

        var nonOwnerResponse = await userBClient.GetAsync($"/files/{fileId}");
        nonOwnerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownerResponse = await userAClient.GetAsync($"/files/{fileId}");
        ownerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CrossUser_Download_Returns404ForNonOwner()
    {
        var userAId = E2ETestHelpers.UniqueUserId();
        var userBId = E2ETestHelpers.UniqueUserId();
        var userAClient = _factory.CreateClientAs(userAId);
        var userBClient = _factory.CreateClientAs(userBId);

        var createResult = await CreateFileAsUser(userAClient);
        var fileId = createResult.File.Id;

        var nonOwnerResponse = await userBClient.GetAsync($"/files/{fileId}/content");
        nonOwnerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossUser_Update_Returns404ForNonOwner()
    {
        var userAId = E2ETestHelpers.UniqueUserId();
        var userBId = E2ETestHelpers.UniqueUserId();
        var userAClient = _factory.CreateClientAs(userAId);
        var userBClient = _factory.CreateClientAs(userBId);

        var createResult = await CreateFileAsUser(userAClient);
        var fileId = createResult.File.Id;

        var updateRecord = new File
        {
            FileName = "hijacked.txt",
            ContentType = "text/plain",
            SizeBytes = 200,
        };
        var nonOwnerResponse = await userBClient.PutAsJsonAsync($"/files/{fileId}", updateRecord);
        nonOwnerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossUser_Delete_Returns404ForNonOwner()
    {
        var userAId = E2ETestHelpers.UniqueUserId();
        var userBId = E2ETestHelpers.UniqueUserId();
        var userAClient = _factory.CreateClientAs(userAId);
        var userBClient = _factory.CreateClientAs(userBId);

        var createResult = await CreateFileAsUser(userAClient);
        var fileId = createResult.File.Id;

        var nonOwnerResponse = await userBClient.DeleteAsync($"/files/{fileId}");
        nonOwnerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
