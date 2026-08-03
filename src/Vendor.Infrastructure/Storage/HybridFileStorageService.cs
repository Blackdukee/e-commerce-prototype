using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Storage;

public class HybridFileStorageService : IFileStorageService
{
    private readonly IFileStorageService _localStorageService;
    private readonly IFileStorageService? _s3StorageService;

    public HybridFileStorageService(IFileStorageService localStorageService, IFileStorageService? s3StorageService = null)
    {
        _localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
        _s3StorageService = s3StorageService;
    }

    private IFileStorageService ActiveService => _s3StorageService ?? _localStorageService;

    public Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        return ActiveService.UploadFileAsync(stream, fileName, contentType, ct);
    }

    public Task<string> GeneratePresignedUploadUrlAsync(string fileName, string contentType, TimeSpan expiration, CancellationToken ct = default)
    {
        return ActiveService.GeneratePresignedUploadUrlAsync(fileName, contentType, expiration, ct);
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        return ActiveService.DeleteFileAsync(fileUrl, ct);
    }
}
