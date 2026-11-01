using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileOwnershipTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FileOwnershipTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
    }

    private async Task<File> CreateFileAsUser(HttpClient client, string fileName = "test.txt")
    {
        var input = new CreateFileInput
        {
            FileName = fileName,
            ContentType = "text/plain",
            SizeBytes = 100,
        };
        var response = await client.PostAsJsonAsync("/file", input);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        return result!.File;
    }

    [Fact]
    public async Task Get_FileOwnedByOtherUser_Returns404()
    {
        var userAClient = _factory.CreateClientAs("user-a");
        var userBClient = _factory.CreateClientAs("user-b");

        var file = await CreateFileAsUser(userAClient);

        var response = await userBClient.GetAsync($"/file/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_FileOwner_Returns200()
    {
        var userAClient = _factory.CreateClientAs("user-a");

        var file = await CreateFileAsUser(userAClient);

        var response = await userAClient.GetAsync($"/file/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_FileOwnedByOtherUser_Returns404()
    {
        var userAClient = _factory.CreateClientAs("user-a");
        var userBClient = _factory.CreateClientAs("user-b");

        var file = await CreateFileAsUser(userAClient);

        var response = await userBClient.GetAsync($"/file/{file.Id}/content");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_FileOwner_Returns200()
    {
        var userAClient = _factory.CreateClientAs("user-a");

        var file = await CreateFileAsUser(userAClient);

        var response = await userAClient.GetAsync($"/file/{file.Id}/content");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_FileOwnedByOtherUser_Returns404()
    {
        var userAClient = _factory.CreateClientAs("user-a");
        var userBClient = _factory.CreateClientAs("user-b");

        var file = await CreateFileAsUser(userAClient);
        var updatedRecord = new File
        {
            FileName = "updated-by-b.txt",
            ContentType = "text/plain",
            SizeBytes = 200,
        };

        var response = await userBClient.PutAsJsonAsync($"/file/{file.Id}", updatedRecord);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_FileOwner_Returns200()
    {
        var userAClient = _factory.CreateClientAs("user-a");

        var file = await CreateFileAsUser(userAClient);
        var updatedRecord = new File
        {
            FileName = "updated-by-a.txt",
            ContentType = "text/plain",
            SizeBytes = 200,
        };

        var response = await userAClient.PutAsJsonAsync($"/file/{file.Id}", updatedRecord);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_FileOwnedByOtherUser_Returns404()
    {
        var userAClient = _factory.CreateClientAs("user-a");
        var userBClient = _factory.CreateClientAs("user-b");

        var file = await CreateFileAsUser(userAClient);

        var response = await userBClient.DeleteAsync($"/file/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_FileOwner_Returns204()
    {
        var userAClient = _factory.CreateClientAs("user-a");

        var file = await CreateFileAsUser(userAClient);

        var response = await userAClient.DeleteAsync($"/file/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task List_DoesNotIncludeOtherUsersFiles()
    {
        var userAClient = _factory.CreateClientAs("user-a");
        var userBClient = _factory.CreateClientAs("user-b");

        await CreateFileAsUser(userAClient, "user-a-file.txt");

        var response = await userBClient.GetAsync("/file");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var files = await response.Content.ReadFromJsonAsync<List<File>>();
        files.Should().NotBeNull();
        files.Should().BeEmpty();
    }
}
