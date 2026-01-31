using System.Collections.Concurrent;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.Files;

namespace ProjectDoomsdayServer.Infrastructure.Files;

public sealed class InMemoryFileRepository : IFileRepository
{
    private readonly ConcurrentDictionary<Guid, FileRecord> _db = new();

    public Task<FileRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_db.TryGetValue(id, out var rec) ? rec : null);

    public Task<IReadOnlyList<FileRecord>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        var list = _db.Values.OrderByDescending(f => f.UpdatedAtUtc).Skip(skip).Take(take).ToList();
        return Task.FromResult<IReadOnlyList<FileRecord>>(list);
    }

    public Task UpsertAsync(FileRecord file, CancellationToken cancellationToken)
    {
        _db[file.Id] = file;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _db.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
