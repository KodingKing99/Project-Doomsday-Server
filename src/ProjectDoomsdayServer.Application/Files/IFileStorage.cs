namespace ProjectDoomsdayServer.Application.Files;

public interface IFileStorage
{
    Task SaveAsync(string id, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string id, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
    Task<bool> ExistsAsync(string id, CancellationToken ct);
    Task<string> GetPresignedUploadUrlAsync(string fileName, CancellationToken cancellationToken);
}
