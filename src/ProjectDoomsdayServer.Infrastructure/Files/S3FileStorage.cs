using Amazon.S3;
using Amazon.S3.Model;
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

    // Generate a presigned PUT URL that a client can use to upload directly to S3.
    // The generated S3 key is prefixed with a GUID to avoid collisions. If you
    // want the server to track the generated key, consider returning the key
    // alongside the URL or accepting an explicit key from the caller.
    public Task<string> GetPresignedUploadUrlAsync(string fileName, CancellationToken cancellationToken)
    {
        // Respect cancellation early
        cancellationToken.ThrowIfCancellationRequested();

        var bucketName = _config.BucketName;

        // Use a GUID prefix to avoid name collisions and make the key unique.
        var key = $"{Guid.NewGuid():N}_{fileName}";

        // Default expiry for the presigned URL. Make configurable via S3Config if desired.
        var expiry = TimeSpan.FromMinutes(15);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry)
            // Optionally set ContentType or other headers to restrict uploads.
        };

        var url = _s3Client.GetPreSignedURL(request);

        return Task.FromResult(url);
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