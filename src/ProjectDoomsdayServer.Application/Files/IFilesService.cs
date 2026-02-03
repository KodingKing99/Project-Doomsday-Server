using ProjectDoomsdayServer.Domain.Files;

namespace ProjectDoomsdayServer.Application.Files;

public interface IFilesService
{
    Task<FileRecord> UpsertAsync(FileRecord record, CancellationToken cancellationToken);
    Task<FileRecord?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<FileRecord>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    );
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(Guid id, CancellationToken cancellationToken);
    Task<string> GetPresignedUploadUrlAsync(string fileName, CancellationToken cancellationToken);
}
