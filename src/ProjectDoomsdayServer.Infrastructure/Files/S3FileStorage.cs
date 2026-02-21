using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.Configuration;

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

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _config.BucketName,
            Key = key,
            InputStream = content,
        };
        await _s3Client.PutObjectAsync(request, ct);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var response = await _s3Client.GetObjectAsync(_config.BucketName, key, ct);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        await _s3Client.DeleteObjectAsync(_config.BucketName, key, ct);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_config.BucketName, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task<string> GetPresignedUploadUrlAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Use HTTP for presigned URLs when the service endpoint is HTTP (e.g. MinIO in tests).
        // The SDK otherwise defaults to HTTPS regardless of the ServiceURL scheme.
        var protocol = _s3Client.Config.ServiceURL?.StartsWith("http://") == true
            ? Protocol.HTTP
            : Protocol.HTTPS;

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _config.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(15),
            Protocol = protocol,
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }
}
