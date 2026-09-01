using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Application.Files.Requests;

public record CreateFileResult(File File, string UploadUrl);
