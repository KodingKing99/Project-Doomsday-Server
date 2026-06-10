namespace ProjectDoomsdayServer.Domain.Models.Input;

public sealed class CreateFileInput
{
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string? HashSha256 { get; set; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
