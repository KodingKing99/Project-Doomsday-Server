using ProjectDoomsdayServer.Application.Interfaces;

namespace ProjectDoomsdayServer.Application.Files.Requests;

public class ExportFileToCsvRequest : IAuthenticatedRequest
{
    public required string FileId { get; set; }
    public required string AuthenticatedUserId { get; set; }
}
