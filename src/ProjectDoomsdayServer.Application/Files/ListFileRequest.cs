using ProjectDoomsdayServer.Application.Interfaces;

namespace ProjectDoomsdayServer.Application.Files;

public class ListFileRequest : IAuthenticatedRequest
{
    public int Skip { get; set; }
    public int Take { get; set; }
    public required string AuthenticatedUserId { get; set; }
}
