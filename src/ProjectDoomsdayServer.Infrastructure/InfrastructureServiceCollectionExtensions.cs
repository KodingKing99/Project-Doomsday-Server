using Microsoft.Extensions.DependencyInjection;

namespace ProjectDoomsdayServer.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register infrastructure services here (e.g., S3-backed storage, repositories)
        // Example: services.AddScoped<IFileStorage, S3FileStorage>();
        return services;
    }
}
