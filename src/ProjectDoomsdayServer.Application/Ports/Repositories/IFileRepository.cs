using ProjectDoomsdayServer.Application.Files;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Application.Ports.Repositories;

/// <summary>
/// Persistence port for file records. Get returns by id only; ownership checks are the caller's responsibility.
/// </summary>
public interface IFileRepository
{
    /// <summary>Returns the file with the given id, or null if it does not exist.</summary>
    Task<File?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>Returns files belonging to the user specified in the request, sorted by most recently updated.</summary>
    Task<IReadOnlyList<File>> ListAsync(
        ListFileRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>Persists a new file record and returns it with the generated id.</summary>
    Task<File> CreateAsync(File file, CancellationToken cancellationToken);

    /// <summary>Replaces the stored file record identified by <see cref="File.Id"/>.</summary>
    Task UpdateAsync(File file, CancellationToken cancellationToken);

    /// <summary>Removes the file record with the given id.</summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
