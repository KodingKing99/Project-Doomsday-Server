using Amazon.S3;
using Amazon.S3.Model;
using MongoDB.Driver;
using Testcontainers.Minio;
using File = ProjectDoomsdayServer.Domain.DB_Models.File;

namespace ProjectDoomsdayServer.E2ETests.Infrastructure;

public sealed class E2EInfrastructureFixture : IAsyncLifetime
{
    private const string DefaultMongoConnectionString = "mongodb://localhost:27017";
    private const string MongoConnectionStringEnvVar = "E2E_MONGO_CONNECTION_STRING";

#pragma warning disable CS0618 // obsolete parameterless constructors — image overridden via WithImage
    private readonly MinioContainer _minio = new MinioBuilder().Build();
#pragma warning restore CS0618

    private MongoClient _mongoClient = default!;

    public string MongoConnectionString { get; private set; } = default!;
    public string DatabaseName { get; } = $"e2e-tests-{Guid.NewGuid():N}";
    public string MinioEndpoint { get; private set; } = default!;
    public string MinioAccessKey { get; private set; } = default!;
    public string MinioSecretKey { get; private set; } = default!;
    public const string BucketName = "e2e-test-bucket";

    public async Task InitializeAsync()
    {
        MongoConnectionString =
            Environment.GetEnvironmentVariable(MongoConnectionStringEnvVar)
            ?? DefaultMongoConnectionString;

        _mongoClient = new MongoClient(MongoConnectionString);

        await _minio.StartAsync();

        MinioEndpoint = _minio.GetConnectionString();
        MinioAccessKey = _minio.GetAccessKey();
        MinioSecretKey = _minio.GetSecretKey();

        using var s3 = BuildS3Client();
        await s3.PutBucketAsync(new PutBucketRequest { BucketName = BucketName });
    }

    public async Task DisposeAsync()
    {
        await _mongoClient.DropDatabaseAsync(DatabaseName);
        await _minio.DisposeAsync();
    }

    /// <summary>
    /// Inserts the given file records into the per-run database's "files" collection.
    /// Call this from test setup before the first request to pre-populate data.
    /// </summary>
    public async Task SeedFilesAsync(IEnumerable<File> files)
    {
        var collection = _mongoClient.GetDatabase(DatabaseName).GetCollection<File>("files");
        await collection.InsertManyAsync(files);
    }

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
