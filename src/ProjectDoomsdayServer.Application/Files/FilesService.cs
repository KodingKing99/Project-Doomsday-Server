using ProjectDoomsdayServer.Domain.Files;

namespace ProjectDoomsdayServer.Application.Files;

public sealed class FileService
{
    private readonly IFileRepository _repo;
    private readonly IFileStorage _storage;

    public FileService(IFileRepository repo, IFileStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<FileRecord> UploadAsync(string fileName, string contentType, Stream content, CancellationToken cancellationToken)
    {
        var record = new FileRecord { FileName = fileName, ContentType = contentType, UpdatedAtUtc = DateTimeOffset.UtcNow };

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        record.SizeBytes = ms.Length;

        ms.Position = 0;
        await _storage.SaveAsync(record.Id, ms, cancellationToken);
        await _repo.UpsertAsync(record, cancellationToken);
        return record;
    }

    public Task<FileRecord?> GetAsync(Guid id, CancellationToken cancellationToken) => _repo.GetAsync(id, cancellationToken);
    public Task<IReadOnlyList<FileRecord>> ListAsync(int skip, int take, CancellationToken cancellationToken) => _repo.ListAsync(skip, take, cancellationToken);
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken) { await _storage.DeleteAsync(id, cancellationToken); await _repo.DeleteAsync(id, cancellationToken); }
    public async Task UpdateMetadataAsync(Guid id, Dictionary<string,string> metadata, CancellationToken cancellationToken)
    {
        var rec = await _repo.GetAsync(id, cancellationToken) ?? throw new KeyNotFoundException("File not found");
        foreach (var kv in metadata) rec.Metadata[kv.Key] = kv.Value;
        rec.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _repo.UpsertAsync(rec, cancellationToken);
    }
    public Task<Stream> DownloadAsync(Guid id, CancellationToken cancellationToken) => _storage.OpenReadAsync(id, cancellationToken);
}
