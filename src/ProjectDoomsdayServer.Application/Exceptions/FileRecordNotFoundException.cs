namespace ProjectDoomsdayServer.Application.Exceptions;

public sealed class FileRecordNotFoundException : Exception
{
    public FileRecordNotFoundException(string message)
        : base(message) { }
}
