using Amazon.S3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using ProjectDoomsdayServer.Domain.Configuration;

namespace ProjectDoomsdayServer.E2ETests.Infrastructure;

public sealed class E2EWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly E2EInfrastructureFixture _infra;

    public E2EWebApplicationFactory(E2EInfrastructureFixture infra) => _infra = infra;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // 1. Replace IAmazonS3 with MinIO-pointing client
            RemoveAllDescriptors<IAmazonS3>(services);
            services.AddSingleton<IAmazonS3>(_ =>
                new AmazonS3Client(
                    _infra.MinioAccessKey,
                    _infra.MinioSecretKey,
                    new AmazonS3Config
                    {
                        ServiceURL = _infra.MinioEndpoint,
                        ForcePathStyle = true,
                        AuthenticationRegion = "us-east-1",
                    }
                )
            );

            // 2. Override S3 bucket name
            services.PostConfigure<S3Config>(cfg => cfg.BucketName = E2EInfrastructureFixture.BucketName);

            // 3. Replace IMongoClient with container-pointing client
            RemoveAllDescriptors<IMongoClient>(services);
            services.AddSingleton<IMongoClient>(_ => new MongoClient(_infra.MongoConnectionString));

            // 4. Override MongoDB database name
            services.PostConfigure<MongoDbConfig>(cfg => cfg.DatabaseName = "e2e-tests");
        });

        builder.UseSetting("Authentication:Enabled", "false");
    }

    private static void RemoveAllDescriptors<T>(IServiceCollection services)
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(T)).ToList())
            services.Remove(d);
    }
}
