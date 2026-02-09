namespace ProjectDoomsdayServer.Domain.DB_Models;

public sealed class File
{
    public string? Id { get; set; }

    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string? HashSha256 { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Metadata { get; init; } = new();
}
