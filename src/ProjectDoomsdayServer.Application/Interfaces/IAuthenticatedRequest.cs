namespace ProjectDoomsdayServer.Application.Interfaces;

public interface IAuthenticatedRequest
{
    public string AuthenticatedUserId { get; set; }
}
