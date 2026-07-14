namespace ProjectDoomsdayServer.Application.Interfaces;

/// <summary>Marks a request as requiring an authenticated user context, carrying the caller's resolved user id.</summary>
public interface IAuthenticatedRequest
{
    public string AuthenticatedUserId { get; set; }
}
