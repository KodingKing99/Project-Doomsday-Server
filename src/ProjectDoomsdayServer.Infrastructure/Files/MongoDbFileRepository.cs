using MongoDB.Driver;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.DB_Models;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Infrastructure.Files;

public sealed class MongoDbFileRepository : IFileRepository
{
    private readonly IMongoCollection<File> _collection;

    public MongoDbFileRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<File>("files");
    }

    public async Task<File?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var filter = Builders<File>.Filter.Eq(f => f.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<File>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        return await _collection
            .Find(FilterDefinition<File>.Empty)
            .SortByDescending(f => f.UpdatedAtUtc)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(File file, CancellationToken cancellationToken)
    {
        var filter = Builders<File>.Filter.Eq(f => f.Id, file.Id);
        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, file, options, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var filter = Builders<File>.Filter.Eq(f => f.Id, id);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }
}
