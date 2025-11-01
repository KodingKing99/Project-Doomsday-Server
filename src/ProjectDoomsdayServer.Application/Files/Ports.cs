using ProjectDoomsdayServer.Domain.Files;

namespace ProjectDoomsdayServer.Application.Files;

public interface IFileStorage
{
    Task SaveAsync(Guid id, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
}

public interface IFileRepository
{
    Task<FileRecord?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<FileRecord>> ListAsync(int skip, int take, CancellationToken ct);
    Task UpsertAsync(FileRecord file, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}