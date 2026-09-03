using ProjectDoomsdayServer.Application.Files.Requests;

namespace ProjectDoomsdayServer.Application.Interfaces;

public interface IFileDescriptionService
{
    Task<string> ExtractTextFromImage(ExtractTextFromImageRequest request, CancellationToken ct);
}
