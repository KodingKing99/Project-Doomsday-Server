using ProjectDoomsdayServer.Application.Interfaces;
using ProjectDoomsdayServer.Domain.Models.Input;

namespace ProjectDoomsdayServer.Application.Files.Requests;

public class CreateFileRequest : IAuthenticatedRequest
{
    public required CreateFileInput Input { get; set; }
    public required string AuthenticatedUserId { get; set; }
}
