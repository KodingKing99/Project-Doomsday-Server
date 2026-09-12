using ProjectDoomsdayServer.Application.Files.Requests;
using ProjectDoomsdayServer.Application.Interfaces;
using DbFile = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Application.Files.Services;

public sealed class CsvExportService : ICsvExportService
{
    private readonly IFilesService _filesService;

    private readonly DbFile testFile = new DbFile
    {
        ContentType = "jpg",
        CreatedAtUtc = DateTime.UtcNow,
        FileName = "mcd_example",
        Id = "1234",
        SizeBytes = 256,
        UserId = "1234",
        StorageKey = "1234",
    };

    public CsvExportService(IFilesService filesService)
    {
        _filesService = filesService;
    }

    public async Task ToCsv(ExportFileToCsvRequest request, CancellationToken cancellationToken)
    {
        // var file = await _filesService.GetAsync(
        //     new GetFileRequest
        //     {
        //         AuthenticatedUserId = request.AuthenticatedUserId,
        //         Id = request.FileId,
        //     },
        //     cancellationToken
        // );
        throw new NotImplementedException();
    }
}
