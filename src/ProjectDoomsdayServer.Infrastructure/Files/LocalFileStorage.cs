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

    private string PathFor(string key)
    {
        var path = Path.Combine(_root, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        var path = PathFor(key);
        using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
            throw new FileNotFoundException();
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct) =>
        Task.FromResult(File.Exists(PathFor(key)));

    public Task<string> GetPresignedUploadUrlAsync(string key, CancellationToken ct)
    {
        throw new NotSupportedException("Local storage does not support presigned URLs.");
    }
}
