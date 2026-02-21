using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using Testcontainers.Minio;
using Testcontainers.MongoDb;

namespace ProjectDoomsdayServer.E2ETests.Infrastructure;

public sealed class E2EInfrastructureFixture : IAsyncLifetime
{
#pragma warning disable CS0618 // obsolete parameterless constructors — image overridden via WithImage
    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder().Build();
#pragma warning restore CS0618

    public string MongoConnectionString { get; private set; } = default!;
    public string MinioEndpoint { get; private set; } = default!;
    public string MinioAccessKey { get; private set; } = default!;
    public string MinioSecretKey { get; private set; } = default!;
    public const string BucketName = "e2e-test-bucket";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mongo.StartAsync(), _minio.StartAsync());

        MongoConnectionString = _mongo.GetConnectionString();
        MinioEndpoint = _minio.GetConnectionString();
        MinioAccessKey = _minio.GetAccessKey();
        MinioSecretKey = _minio.GetSecretKey();

        using var s3 = BuildS3Client();
        await s3.PutBucketAsync(new PutBucketRequest { BucketName = BucketName });
    }

    public async Task DisposeAsync() =>
        await Task.WhenAll(_mongo.DisposeAsync().AsTask(), _minio.DisposeAsync().AsTask());

    public AmazonS3Client BuildS3Client() =>
        new AmazonS3Client(
            MinioAccessKey,
            MinioSecretKey,
            new AmazonS3Config
            {
                ServiceURL = MinioEndpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            }
        );
}
