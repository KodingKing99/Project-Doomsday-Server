using ProjectDoomsdayServer.Domain.Files;

namespace ProjectDoomsdayServer.Application.Files;

public interface IFileRepository
{
    Task<FileRecord?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<FileRecord>> ListAsync(int skip, int take, CancellationToken ct);
    Task UpsertAsync(FileRecord file, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
