namespace ProjectDoomsdayServer.Domain.Configuration;

public class MongoDbConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "ProjectDoomsday";
}
