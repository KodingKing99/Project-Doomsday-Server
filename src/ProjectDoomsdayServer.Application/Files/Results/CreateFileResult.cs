using File = ProjectDoomsdayServer.Domain.DbModels.File;

namespace ProjectDoomsdayServer.Application.Files.Results;

public record CreateFileResult(File File, string UploadUrl);
