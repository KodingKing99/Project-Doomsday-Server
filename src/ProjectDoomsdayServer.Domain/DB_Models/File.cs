using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectDoomsdayServer.Domain.DB_Models;

public sealed class File
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; init; } = Guid.NewGuid();

    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string? HashSha256 { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Metadata { get; init; } = new();
}
