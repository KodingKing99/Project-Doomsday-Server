using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.Application.Files;

public record CreateFileResult(File File, string UploadUrl);
