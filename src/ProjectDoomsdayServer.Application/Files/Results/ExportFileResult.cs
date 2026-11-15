namespace ProjectDoomsdayServer.Application.Files.Results;

public record ExportFileResult(byte[] Content, string ContentType, string FileName);
