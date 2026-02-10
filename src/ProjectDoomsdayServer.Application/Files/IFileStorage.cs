namespace ProjectDoomsdayServer.Application.Files;

public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
    Task<string> GetPresignedUploadUrlAsync(string key, CancellationToken ct);
}
