using ProjectDoomsdayServer.Application.Interfaces;

namespace ProjectDoomsdayServer.Application.Files.Requests;

public class ExtractTextFromImageRequest : IAuthenticatedRequest
{
    // TODO: may have this take a file stream of the file blob instead. No dependencies on the file service
    // this will likely be implemented in  doomsday.infrastructure
    public required string FileId { get; set; }
    public required string AuthenticatedUserId { get; set; }
}
