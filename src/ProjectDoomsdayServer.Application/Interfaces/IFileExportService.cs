using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Files.Results;

namespace ProjectDoomsdayServer.Application.Interfaces;

/// <summary>
/// Exports extracted expense information of a stored file as a downloadable document.
/// </summary>
public interface IFileExportService
{
    /// <summary>
    /// Builds a document with one row per line item (or one row for an expense without line items).
    /// Throws <see cref="Exceptions.FileRecordNotFoundException"/> if the file is missing or not owned by the user.
    /// Returns a header-only document when no extraction exists yet.
    /// </summary>
    Task<ExportFileResult> ExportFile(
        ExportFileRequest request,
        CancellationToken cancellationToken
    );
}
