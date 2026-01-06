namespace ProjectDoomsdayServer.Application.Files;
public interface IFileStorage
{
    Task SaveAsync(Guid id, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    Task<string> GetPresignedUploadUrlAsync(string fileName, CancellationToken cancellationToken);
}