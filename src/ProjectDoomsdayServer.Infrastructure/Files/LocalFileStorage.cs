using ProjectDoomsdayServer.Application.Files;

namespace ProjectDoomsdayServer.Infrastructure.Files;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage()
    {
        _root = "var/storage";
        Directory.CreateDirectory(_root);
    }

    private string PathFor(Guid id) => Path.Combine(_root, id.ToString("N"));

    public async Task SaveAsync(Guid id, Stream content, CancellationToken ct)
    {
        var path = PathFor(id);
        using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream> OpenReadAsync(Guid id, CancellationToken ct)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
            throw new FileNotFoundException();
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var path = PathFor(id);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(File.Exists(PathFor(id)));

    public Task<string> GetPresignedUploadUrlAsync(
        string fileName,
        CancellationToken cancellationToken
    )
    {
        throw new NotImplementedException();
    }
}
