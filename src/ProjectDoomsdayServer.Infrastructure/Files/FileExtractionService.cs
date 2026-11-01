using Amazon.Textract;
using ProjectDoomsdayServer.Application.Interfaces;
using ProjectDoomsdayServer.Application.Ports.Services.TextExtraction;
using ProjectDoomsdayServer.Domain.Models.TextExtraction;

namespace ProjectDoomsdayServer.Infrastructure.Files;

public sealed class FileExtractionService : IFileTextExtractionService
{
    private readonly IAmazonTextract _awsTextractClient;
    private readonly TimeProvider _timeProvider;

    public FileExtractionService(IAmazonTextract awsTextractClient, TimeProvider timeProvider)
    {
        _awsTextractClient = awsTextractClient;
        _timeProvider = timeProvider;
    }

    public async Task<ExtractedExpenseInfo> ExtractExpenseInformationAsync(
        MemoryStream stream,
        CancellationToken cancellationToken
    )
    {
        var response = await _awsTextractClient.AnalyzeExpenseAsync(
            new() { Document = new() { Bytes = stream } },
            cancellationToken
        );
        return TextractExpenseMapper.Map(response, _timeProvider.GetUtcNow());
    }
}
