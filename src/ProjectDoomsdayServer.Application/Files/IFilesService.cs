using ProjectDoomsdayServer.Application.Exceptions;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Application.Files;

/// <summary>
/// Orchestrates file operations, enforcing per-user ownership on all reads and writes.
/// </summary>
public interface IFilesService
{
    /// <summary>
    /// Creates a new file record, generates the storage key, and returns a presigned upload URL.
    /// </summary>
    Task<CreateFileResult> CreateAsync(
        CreateFileRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Returns the file owned by the authenticated user, or throws <see cref="FileRecordNotFoundException"/> if missing or not owned.
    /// </summary>
    Task<File> GetAsync(GetFileRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns all files belonging to the authenticated user, paginated and sorted by most recently updated.
    /// </summary>
    Task<IReadOnlyList<File>> ListAsync(
        ListFileRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates mutable fields on a file the authenticated user owns. Server-controlled fields (UserId, StorageKey, Id, CreatedAtUtc) are preserved.
    /// Throws <see cref="FileRecordNotFoundException"/> if missing or not owned.
    /// </summary>
    Task<File> UpdateAsync(UpdateFileRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a file owned by the authenticated user from both storage and the repository.
    /// Throws <see cref="FileRecordNotFoundException"/> if missing or not owned.
    /// </summary>
    Task DeleteAsync(DeleteFileRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a read stream for a file owned by the authenticated user.
    /// Throws <see cref="FileRecordNotFoundException"/> if missing, not owned, or has no storage key.
    /// </summary>
    Task<Stream> DownloadAsync(DownloadFileRequest request, CancellationToken cancellationToken);
}
