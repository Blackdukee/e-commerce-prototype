using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Storage;

public class LocalStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly string _baseUrl;

    public LocalStorageService(string? rootPath = null, string baseUrl = "/uploads")
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads")
            : rootPath;
        _baseUrl = baseUrl;
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_rootPath);
        var cleanFileName = Path.GetFileName(fileName);
        var filePath = Path.Combine(_rootPath, cleanFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await stream.CopyToAsync(fileStream, ct);
        }

        return FormatFileUrl(cleanFileName);
    }

    public Task<string> GeneratePresignedUploadUrlAsync(string fileName, string contentType, TimeSpan expiration, CancellationToken ct = default)
    {
        var cleanFileName = Path.GetFileName(fileName);
        return Task.FromResult(FormatFileUrl(cleanFileName));
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        var cleanFileName = Path.GetFileName(fileUrl);
        if (!string.IsNullOrEmpty(cleanFileName))
        {
            var filePath = Path.Combine(_rootPath, cleanFileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        return Task.CompletedTask;
    }

    private string FormatFileUrl(string fileName)
    {
        var prefix = string.IsNullOrWhiteSpace(_baseUrl) ? "/uploads" : _baseUrl.TrimEnd('/');
        return $"{prefix}/{fileName}";
    }
}
