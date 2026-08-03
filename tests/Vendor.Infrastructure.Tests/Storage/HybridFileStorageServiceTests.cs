using System.Text;
using FluentAssertions;
using Moq;
using Vendor.Application.Common.Interfaces;
using Vendor.Infrastructure.Storage;
using Xunit;

namespace Vendor.Infrastructure.Tests.Storage;

public class HybridFileStorageServiceTests : IDisposable
{
    private readonly string _tempFolder;

    public HybridFileStorageServiceTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "vendor_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
        {
            try
            {
                Directory.Delete(_tempFolder, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in test dispose
            }
        }
    }

    [Fact]
    public async Task LocalStorageFallback_GeneratesValidUploadUrl()
    {
        var localService = new LocalStorageService(rootPath: _tempFolder);
        var hybridService = new HybridFileStorageService(localService, s3StorageService: null);

        var url = await hybridService.GeneratePresignedUploadUrlAsync("test_image.png", "image/png", TimeSpan.FromMinutes(15));
        
        url.Should().NotBeNullOrEmpty();
        url.Should().Contain("test_image.png");
    }

    [Fact]
    public async Task LocalStorage_UploadAndDeleteFile_WorksCorrectly()
    {
        var localService = new LocalStorageService(rootPath: _tempFolder);
        var hybridService = new HybridFileStorageService(localService, s3StorageService: null);

        var fileName = "sample.txt";
        var content = "Hello World Storage Test";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var fileUrl = await hybridService.UploadFileAsync(stream, fileName, "text/plain");

        fileUrl.Should().NotBeNullOrEmpty();
        var filePath = Path.Combine(_tempFolder, fileName);
        File.Exists(filePath).Should().BeTrue();
        (await File.ReadAllTextAsync(filePath)).Should().Be(content);

        await hybridService.DeleteFileAsync(fileUrl);
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task HybridStorage_UsesS3Service_WhenS3Configured()
    {
        var localService = new LocalStorageService(rootPath: _tempFolder);
        var mockS3Service = new Mock<IFileStorageService>();
        mockS3Service
            .Setup(s => s.GeneratePresignedUploadUrlAsync("s3_file.png", "image/png", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://my-bucket.s3.amazonaws.com/s3_file.png?presigned=true");

        var hybridService = new HybridFileStorageService(localService, mockS3Service.Object);

        var url = await hybridService.GeneratePresignedUploadUrlAsync("s3_file.png", "image/png", TimeSpan.FromMinutes(15));

        url.Should().Be("https://my-bucket.s3.amazonaws.com/s3_file.png?presigned=true");
        mockS3Service.Verify(s => s.GeneratePresignedUploadUrlAsync("s3_file.png", "image/png", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HybridStorage_UploadFile_DelegatesToS3_WhenConfigured()
    {
        var localService = new LocalStorageService(rootPath: _tempFolder);
        var mockS3Service = new Mock<IFileStorageService>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test data"));

        mockS3Service
            .Setup(s => s.UploadFileAsync(stream, "test.txt", "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://my-bucket.s3.amazonaws.com/test.txt");

        var hybridService = new HybridFileStorageService(localService, mockS3Service.Object);

        var resultUrl = await hybridService.UploadFileAsync(stream, "test.txt", "text/plain");

        resultUrl.Should().Be("https://my-bucket.s3.amazonaws.com/test.txt");
        mockS3Service.Verify(s => s.UploadFileAsync(stream, "test.txt", "text/plain", It.IsAny<CancellationToken>()), Times.Once);
    }
}
