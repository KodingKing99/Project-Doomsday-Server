using ProjectDoomsdayServer.Application.Exceptions;
using ProjectDoomsdayServer.Application.Ports.Repositories;
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
        CreateFileRequest request,
        CancellationToken cancellationToken
    )
    {
        var input = request.Input;
        var now = DateTime.UtcNow;
        var record = new File
        {
            CreatedAtUtc = now,
            Metadata = input.Metadata,
            ContentType = input.ContentType,
            FileName = input.FileName,
            HashSha256 = input.HashSha256,
            SizeBytes = input.SizeBytes,
            UpdatedAtUtc = now,
            UserId = request.AuthenticatedUserId,
        };
        var created = await _repo.CreateAsync(record, cancellationToken);

        created.StorageKey = $"{created.UserId}/{created.Id}";
        await _repo.UpdateAsync(created, cancellationToken);

        var uploadUrl = await _storage.GetPresignedUploadUrlAsync(
            created.StorageKey,
            cancellationToken
        );

        return new CreateFileResult(created, uploadUrl);
    }

    public async Task<File> GetAsync(GetFileRequest request, CancellationToken cancellationToken)
    {
        var record = await _repo.GetAsync(request.Id, cancellationToken);
        if (record is null || record.UserId != request.AuthenticatedUserId)
            throw new FileRecordNotFoundException($"File {request.Id} not found.");
        return record;
    }

    public Task<IReadOnlyList<File>> ListAsync(
        ListFileRequest request,
        CancellationToken cancellationToken
    ) => _repo.ListAsync(request, cancellationToken);

    public async Task<File> UpdateAsync(
        UpdateFileRequest request,
        CancellationToken cancellationToken
    )
    {
        var existing = await _repo.GetAsync(request.Id, cancellationToken);
        if (existing is null || existing.UserId != request.AuthenticatedUserId)
            throw new FileRecordNotFoundException($"File {request.Id} not found.");

        existing.FileName = request.FileName;
        existing.ContentType = request.ContentType;
        existing.SizeBytes = request.SizeBytes;
        existing.HashSha256 = request.HashSha256;
        if (existing.Metadata is not null)
        {
            existing.Metadata.Clear();
            foreach (var kv in request.Metadata)
                existing.Metadata[kv.Key] = kv.Value;
        }
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _repo.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(DeleteFileRequest request, CancellationToken cancellationToken)
    {
        var file = await _repo.GetAsync(request.Id, cancellationToken);
        if (file is null || file.UserId != request.AuthenticatedUserId)
            throw new FileRecordNotFoundException($"File {request.Id} not found.");

        if (file.StorageKey is not null)
            await _storage.DeleteAsync(file.StorageKey, cancellationToken);
        await _repo.DeleteAsync(request.Id, cancellationToken);
    }

    public async Task<Stream> DownloadAsync(
        DownloadFileRequest request,
        CancellationToken cancellationToken
    )
    {
        var file = await _repo.GetAsync(request.Id, cancellationToken);
        if (file is null || file.UserId != request.AuthenticatedUserId)
            throw new FileRecordNotFoundException($"File {request.Id} not found.");
        if (file.StorageKey is null)
            throw new FileRecordNotFoundException($"File {request.Id} has no storage key.");
        return await _storage.OpenReadAsync(file.StorageKey, cancellationToken);
    }
}
