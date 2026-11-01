using ProjectDoomsdayServer.Application.Files.Requests;

namespace ProjectDoomsdayServer.Application.Interfaces;

/// <summary>
/// Exports extracted expense information of a stored file as CSV.
/// </summary>
public interface ICsvExportService
{
    /// <summary>
    /// Builds a CSV document with one row per line item (or one row for an expense without line items).
    /// Throws <see cref="Exceptions.FileRecordNotFoundException"/> if the file is missing or not owned by the user.
    /// Returns a header-only document when no extraction exists yet.
    /// </summary>
    Task<string> ToCsv(ExportFileToCsvRequest request, CancellationToken cancellationToken);
}
