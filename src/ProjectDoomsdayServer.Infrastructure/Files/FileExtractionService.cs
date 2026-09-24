using System.Text;
using System.Text.Json;
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
        var builder = new StringBuilder();
        foreach (var document in response.ExpenseDocuments)
        {
            builder.Append(JsonSerializer.Serialize(document));
        }
        return builder.ToString();
    }
}
