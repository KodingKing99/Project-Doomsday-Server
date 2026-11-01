using ProjectDoomsdayServer.Domain.Models.TextExtraction;

namespace ProjectDoomsdayServer.Application.Ports.Services.TextExtraction;

public interface IFileTextExtractionService
{
    /// <summary>
    /// Runs expense extraction over an image or PDF of a receipt or invoice.
    /// </summary>
    /// <param name="stream">The document bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The structured extraction result; <c>Expenses</c> is empty when nothing was found.</returns>
    Task<ExtractionInfo> ExtractExpenseInformationAsync(
        MemoryStream stream,
        CancellationToken cancellationToken
    );
}
