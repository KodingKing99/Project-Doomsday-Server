using ProjectDoomsdayServer.Application.Interfaces;

namespace ProjectDoomsdayServer.Application.Files.Requests;

public class DownloadFileRequest : IAuthenticatedRequest
{
    public required string Id { get; set; }
    public required string AuthenticatedUserId { get; set; }
}
