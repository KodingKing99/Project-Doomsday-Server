using System.Collections.Concurrent;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Infrastructure.Files;

public sealed class InMemoryFileRepository : IFileRepository
{
    private readonly ConcurrentDictionary<Guid, File> _db = new();

    public Task<File?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_db.TryGetValue(id, out var rec) ? rec : null);

    public Task<IReadOnlyList<File>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        var list = _db.Values.OrderByDescending(f => f.UpdatedAtUtc).Skip(skip).Take(take).ToList();
        return Task.FromResult<IReadOnlyList<File>>(list);
    }

    public Task UpsertAsync(File file, CancellationToken cancellationToken)
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
