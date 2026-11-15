using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Application.Interfaces;
using ProjectDoomsdayServer.Domain.Configuration;
using ProjectDoomsdayServer.Infrastructure.Files;

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

        services.AddKeyedScoped<IFileExportService, ExcelExportService>(ExportFormats.Excel);

        return services;
    }
}
