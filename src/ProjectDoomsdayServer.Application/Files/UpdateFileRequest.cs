using ProjectDoomsdayServer.Application.Interfaces;

namespace ProjectDoomsdayServer.Application.Files;

public class UpdateFileRequest : IAuthenticatedRequest
{
    public required string Id { get; set; }
    public required string FileName { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string? HashSha256 { get; set; }
    public Dictionary<string, string> Metadata { get; init; } = new();
    public required string AuthenticatedUserId { get; set; }
}
