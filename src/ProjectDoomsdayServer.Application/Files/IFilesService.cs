using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Application.Files;

public interface IFilesService
{
    Task<CreateFileResult> CreateAsync(File record, CancellationToken cancellationToken);
    Task<File> UpdateAsync(File record, CancellationToken cancellationToken);
    Task<File?> GetAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<File>> ListAsync(int skip, int take, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string id, CancellationToken cancellationToken);
}
