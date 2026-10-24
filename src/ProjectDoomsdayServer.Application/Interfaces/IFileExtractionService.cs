using ProjectDoomsdayServer.Domain.Models.Extraction;

namespace ProjectDoomsdayServer.Application.Interfaces;

public interface IFileExtractionService
{
    /// <summary>
    /// Runs expense extraction over an image or PDF of a receipt or invoice.
    /// </summary>
    /// <param name="stream">The document bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The structured extraction result; <c>Expenses</c> is empty when nothing was found.</returns>
    Task<ExtractedExpenseInfo> ExtractExpenseInformationAsync(
        MemoryStream stream,
        CancellationToken cancellationToken
    );
}
