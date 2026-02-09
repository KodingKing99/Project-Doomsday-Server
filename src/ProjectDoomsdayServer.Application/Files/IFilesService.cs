using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Application.Files;

public interface IFilesService
{
    Task<File> UpsertAsync(File record, CancellationToken cancellationToken);
    Task<File?> GetAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<File>> ListAsync(int skip, int take, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string id, CancellationToken cancellationToken);
    Task<string> GetPresignedUploadUrlAsync(string fileName, CancellationToken cancellationToken);
}
