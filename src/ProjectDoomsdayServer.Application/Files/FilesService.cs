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

    public async Task<CreateFileResult> CreateAsync(
        File record,
        CancellationToken cancellationToken
    )
    {
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var created = await _repo.CreateAsync(record, cancellationToken);

        created.StorageKey = $"{created.UserId}/{created.Id}";
        await _repo.UpdateAsync(created, cancellationToken);

        var uploadUrl = await _storage.GetPresignedUploadUrlAsync(
            created.StorageKey,
            cancellationToken
        );

        return new CreateFileResult(created, uploadUrl);
    }

    public async Task<File> UpdateAsync(File record, CancellationToken cancellationToken)
    {
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(record, cancellationToken);
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
        var file = await _repo.GetAsync(id, cancellationToken);
        if (file?.StorageKey is not null)
            await _storage.DeleteAsync(file.StorageKey, cancellationToken);
        await _repo.DeleteAsync(id, cancellationToken);
    }

    public async Task<Stream> DownloadAsync(string id, CancellationToken cancellationToken)
    {
        var file = await _repo.GetAsync(id, cancellationToken);
        if (file?.StorageKey is null)
            throw new FileNotFoundException($"File {id} not found or has no storage key.");
        return await _storage.OpenReadAsync(file.StorageKey, cancellationToken);
    }
}
