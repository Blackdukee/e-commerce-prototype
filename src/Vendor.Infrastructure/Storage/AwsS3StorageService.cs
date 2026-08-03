using Amazon.S3;
using Amazon.S3.Model;
using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Storage;

public class AwsS3StorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public AwsS3StorageService(IAmazonS3 s3Client, string bucketName)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = !string.IsNullOrWhiteSpace(bucketName) ? bucketName : throw new ArgumentNullException(nameof(bucketName));
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var cleanFileName = Path.GetFileName(fileName);
        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = cleanFileName,
            InputStream = stream,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(putRequest, ct);
        return $"https://{_bucketName}.s3.amazonaws.com/{cleanFileName}";
    }

    public Task<string> GeneratePresignedUploadUrlAsync(string fileName, string contentType, TimeSpan expiration, CancellationToken ct = default)
    {
        var cleanFileName = Path.GetFileName(fileName);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = cleanFileName,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiration),
            ContentType = contentType
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public async Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return;

        var key = ExtractKeyFromUrl(fileUrl);
        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        await _s3Client.DeleteObjectAsync(deleteRequest, ct);
    }

    private static string ExtractKeyFromUrl(string fileUrl)
    {
        if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return uri.AbsolutePath.TrimStart('/');
        }
        return Path.GetFileName(fileUrl);
    }
}
