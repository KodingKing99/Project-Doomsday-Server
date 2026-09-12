using ProjectDoomsdayServer.Application.Files.Requests;

namespace ProjectDoomsdayServer.Application.Interfaces;

public interface IFileExtractionService
{
    Task<string> ExtractExpenseInformationAsync(
        MemoryStream stream,
        CancellationToken cancellationToken
    );
}
