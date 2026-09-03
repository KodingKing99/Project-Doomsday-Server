using ProjectDoomsdayServer.Application.Files.Requests;

namespace ProjectDoomsdayServer.Application.Interfaces;

public interface ICsvExportService
{
    Task ToCsv(ExportFileToCsvRequest request, CancellationToken ct);
}
