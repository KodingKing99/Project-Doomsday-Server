using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Application.Files;

public interface IFileRepository
{
    Task<File?> GetAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<File>> ListAsync(int skip, int take, CancellationToken ct);
    Task<File> CreateAsync(File file, CancellationToken ct);
    Task UpdateAsync(File file, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
}
