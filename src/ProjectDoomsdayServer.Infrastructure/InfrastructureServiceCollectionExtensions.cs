using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectDoomsdayServer.Domain.Configuration;

namespace ProjectDoomsdayServer.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IMongoClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<MongoDbConfig>>().Value;
            return new MongoClient(config.ConnectionString);
        });

        return services;
    }
}
