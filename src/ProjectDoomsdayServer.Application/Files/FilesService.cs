using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Application.Files;

public sealed class FilesService : IFilesService
{
    private readonly IFileRepository _repo;
    private readonly IFileStorage _storage;

    public FilesService(IFileRepository repo, IFileStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<File> UpsertAsync(File record, CancellationToken cancellationToken)
    {
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _repo.UpsertAsync(record, cancellationToken);
        return record;
    }

    public Task<File?> GetAsync(string id, CancellationToken cancellationToken) =>
        _repo.GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<File>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    ) => _repo.ListAsync(skip, take, cancellationToken);

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _storage.DeleteAsync(id, cancellationToken);
        await _repo.DeleteAsync(id, cancellationToken);
    }

    public Task<Stream> DownloadAsync(string id, CancellationToken cancellationToken) =>
        _storage.OpenReadAsync(id, cancellationToken);

    public Task<string> GetPresignedUploadUrlAsync(
        string fileName,
        CancellationToken cancellationToken
    ) => _storage.GetPresignedUploadUrlAsync(fileName, cancellationToken);
}
