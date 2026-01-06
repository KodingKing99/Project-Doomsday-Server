using Amazon.S3;
using System.Threading.Tasks;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.Configuration;
using Microsoft.Extensions.Options;

namespace ProjectDoomsdayServer.Infrastructure.Files;

public class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Config _config;
    public S3FileStorage(IAmazonS3 s3Client, IOptions<S3Config> config)
    {
        _s3Client = s3Client;
        _config = config.Value;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    // Example stub method
    public Task<string> GetPresignedUploadUrlAsync(string fileName, CancellationToken cancellationToken)
    {
        var bucketName = _config.BucketName;
        // TODO: Implement presigned URL logic
        throw new NotImplementedException();
    }

    public Task<Stream> OpenReadAsync(Guid id, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task SaveAsync(Guid id, Stream content, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}