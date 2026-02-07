using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Application.Files;

public interface IFileRepository
{
    Task<File?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<File>> ListAsync(int skip, int take, CancellationToken ct);
    Task UpsertAsync(File file, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
