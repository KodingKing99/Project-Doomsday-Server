using System.Collections.Concurrent;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Infrastructure.Files;

public sealed class InMemoryFileRepository : IFileRepository
{
    private readonly ConcurrentDictionary<string, File> _db = new();

    public Task<File?> GetAsync(string id, CancellationToken cancellationToken) =>
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

    public Task<File> CreateAsync(File file, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(file.Id))
            file.Id = Guid.NewGuid().ToString("N");

        _db[file.Id] = file;
        return Task.FromResult(file);
    }

    public Task UpdateAsync(File file, CancellationToken cancellationToken)
    {
        _db[file.Id] = file;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        _db.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
