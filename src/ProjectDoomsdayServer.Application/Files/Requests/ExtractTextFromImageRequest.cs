using ProjectDoomsdayServer.Application.Interfaces;

namespace ProjectDoomsdayServer.Application.Files.Requests;

public class ExtractExpenseImageInfoRequest : IAuthenticatedRequest
{
    public required string AuthenticatedUserId { get; set; }
    public required string Id { get; set; }
}
