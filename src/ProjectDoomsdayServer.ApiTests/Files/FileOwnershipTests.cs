using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ProjectDoomsdayServer.ApiTests.TestSupport;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;
using ProjectDoomsdayServer.Domain.Models.Input;
using ProjectDoomsdayServer.TestSupport;
using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.ApiTests.Files;

public class FileOwnershipTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FileOwnershipTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Reset();
    }

    private static async Task<File> CreateFileAsUser(
        HttpClient client,
        string fileName = TestConstants.DefaultTestFileName
    )
    {
        var input = new CreateFileInput
        {
            FileName = fileName,
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 100,
        };
        var response = await client.PostAsJsonAsync(FileRoutes.Base, input);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateFileResult>();
        return result!.File;
    }

    [Fact]
    public async Task Get_FileOwnedByOtherUser_Returns404()
    {
        // Arrange
        var userAClient = _factory.CreateClientAs(TestConstants.UserA);
        var userBClient = _factory.CreateClientAs(TestConstants.UserB);
        var file = await CreateFileAsUser(userAClient);

        // Act
        var response = await userBClient.GetAsync(FileRoutes.ById(file.Id!));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_FileOwner_Returns200()
    {
        // Arrange
        var userAClient = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsUser(userAClient);

        // Act
        var response = await userAClient.GetAsync(FileRoutes.ById(file.Id!));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_FileOwnedByOtherUser_Returns404()
    {
        // Arrange
        var userAClient = _factory.CreateClientAs(TestConstants.UserA);
        var userBClient = _factory.CreateClientAs(TestConstants.UserB);
        var file = await CreateFileAsUser(userAClient);

        // Act
        var response = await userBClient.GetAsync(FileRoutes.Content(file.Id!));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_FileOwner_Returns200()
    {
        // Arrange
        var userAClient = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsUser(userAClient);

        // Act
        var response = await userAClient.GetAsync(FileRoutes.Content(file.Id!));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_FileOwnedByOtherUser_Returns404()
    {
        // Arrange
        var userAClient = _factory.CreateClientAs(TestConstants.UserA);
        var userBClient = _factory.CreateClientAs(TestConstants.UserB);
        var file = await CreateFileAsUser(userAClient);
        var updatedRecord = new File
        {
            FileName = "updated-by-b.txt",
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 200,
        };

        // Act
        var response = await userBClient.PutAsJsonAsync(FileRoutes.ById(file.Id!), updatedRecord);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_FileOwner_Returns200()
    {
        // Arrange
        var userAClient = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsUser(userAClient);
        var updatedRecord = new File
        {
            FileName = "updated-by-a.txt",
            ContentType = TestConstants.PlainTextContentType,
            SizeBytes = 200,
        };

        // Act
        var response = await userAClient.PutAsJsonAsync(FileRoutes.ById(file.Id!), updatedRecord);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_FileOwnedByOtherUser_Returns404()
    {
        // Arrange
        var userAClient = _factory.CreateClientAs(TestConstants.UserA);
        var userBClient = _factory.CreateClientAs(TestConstants.UserB);
        var file = await CreateFileAsUser(userAClient);

        // Act
        var response = await userBClient.DeleteAsync(FileRoutes.ById(file.Id!));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_FileOwner_Returns204()
    {
        // Arrange
        var userAClient = _factory.CreateClientAs(TestConstants.UserA);
        var file = await CreateFileAsUser(userAClient);

        // Act
        var response = await userAClient.DeleteAsync(FileRoutes.ById(file.Id!));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task List_DoesNotIncludeOtherUsersFiles()
    {
        // Arrange
        var userAClient = _factory.CreateClientAs(TestConstants.UserA);
        var userBClient = _factory.CreateClientAs(TestConstants.UserB);
        await CreateFileAsUser(userAClient, "user-a-file.txt");

        // Act
        var response = await userBClient.GetAsync(FileRoutes.List());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var files = await response.Content.ReadFromJsonAsync<List<File>>();
        files.Should().NotBeNull();
        files.Should().BeEmpty();
    }
}
