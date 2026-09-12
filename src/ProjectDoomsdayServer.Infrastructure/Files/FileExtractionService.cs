using Amazon.Textract;
using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Interfaces;

namespace ProjectDoomsdayServer.Infrastructure.Files;

public sealed class FileExtractionService : IFileExtractionService
{
    private readonly IAmazonTextract _awsTextractClient;

    public FileExtractionService(IAmazonTextract awsTextractClient)
    {
        _awsTextractClient = awsTextractClient;
    }

    public async Task<string> ExtractExpenseInformationAsync(
        MemoryStream stream,
        CancellationToken cancellationToken
    )
    {
        var response = await _awsTextractClient.AnalyzeExpenseAsync(
            new() { Document = new() { Bytes = stream } },
            cancellationToken
        );
        throw new NotImplementedException();
    }
}
