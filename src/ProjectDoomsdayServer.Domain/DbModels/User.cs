namespace ProjectDoomsdayServer.Domain.DbModels;

public class User
{
    public required string Id { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
