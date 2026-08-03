diff --git a/src/Vendor.Api/Endpoints/MediaEndpoints.cs b/src/Vendor.Api/Endpoints/MediaEndpoints.cs
new file mode 100644
index 0000000..7630691
--- /dev/null
+++ b/src/Vendor.Api/Endpoints/MediaEndpoints.cs
@@ -0,0 +1,30 @@
+using Microsoft.AspNetCore.Builder;
+using Microsoft.AspNetCore.Http;
+using Microsoft.AspNetCore.Routing;
+using Vendor.Application.Common.Interfaces;
+
+namespace Vendor.Api.Endpoints;
+
+public static class MediaEndpoints
+{
+    public static RouteGroupBuilder MapMediaEndpoints(this RouteGroupBuilder group)
+    {
+        var media = group.MapGroup("/media").WithTags("Media");
+
+        media.MapGet("/presigned-url", async (string? fileName, string? contentType, int? expirationMinutes, IFileStorageService storageService, CancellationToken ct) =>
+        {
+            if (string.IsNullOrWhiteSpace(fileName))
+            {
+                return Results.BadRequest(new { Error = "fileName query parameter is required." });
+            }
+
+            var effectiveContentType = !string.IsNullOrWhiteSpace(contentType) ? contentType : "application/octet-stream";
+            var expiration = TimeSpan.FromMinutes(expirationMinutes is > 0 ? expirationMinutes.Value : 15);
+
+            var uploadUrl = await storageService.GeneratePresignedUploadUrlAsync(fileName, effectiveContentType, expiration, ct);
+            return Results.Ok(new { Url = uploadUrl, FileName = fileName });
+        });
+
+        return group;
+    }
+}
diff --git a/src/Vendor.Api/Extensions/WebApplicationExtensions.cs b/src/Vendor.Api/Extensions/WebApplicationExtensions.cs
index e90cf46..66a8740 100644
--- a/src/Vendor.Api/Extensions/WebApplicationExtensions.cs
+++ b/src/Vendor.Api/Extensions/WebApplicationExtensions.cs
@@ -29,6 +29,7 @@ public static class WebApplicationExtensions
         v1.MapAdminEndpoints();
         v1.MapVendorSettingsEndpoints();
         v1.MapWebhookEndpoints();
+        v1.MapMediaEndpoints();
 
         // SignalR WebSockets Hub endpoint
         app.MapHub<AdminNotificationHub>("/hubs/admin");
diff --git a/src/Vendor.Application/Common/Interfaces/IFileStorageService.cs b/src/Vendor.Application/Common/Interfaces/IFileStorageService.cs
new file mode 100644
index 0000000..74b55f4
--- /dev/null
+++ b/src/Vendor.Application/Common/Interfaces/IFileStorageService.cs
@@ -0,0 +1,8 @@
+namespace Vendor.Application.Common.Interfaces;
+
+public interface IFileStorageService
+{
+    Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
+    Task<string> GeneratePresignedUploadUrlAsync(string fileName, string contentType, TimeSpan expiration, CancellationToken ct = default);
+    Task DeleteFileAsync(string fileUrl, CancellationToken ct = default);
+}
diff --git a/src/Vendor.Infrastructure/DependencyInjection.cs b/src/Vendor.Infrastructure/DependencyInjection.cs
index 503fd68..7034bab 100644
--- a/src/Vendor.Infrastructure/DependencyInjection.cs
+++ b/src/Vendor.Infrastructure/DependencyInjection.cs
@@ -26,7 +26,7 @@ using Vendor.Infrastructure.Persistence;
 using Vendor.Infrastructure.Persistence.Repositories;
 using Vendor.Infrastructure.Tax;
 using Vendor.Infrastructure.Payments.Webhooks;
-
+using Vendor.Infrastructure.Storage;
 
 namespace Vendor.Infrastructure;
 
@@ -154,6 +154,44 @@ public static class DependencyInjection
         services.AddScoped<IWebhookParser, PaypalWebhookParser>();
         services.AddScoped<IWebhookParserFactory, WebhookParserFactory>();
 
+        // Register File Storage Service (Hybrid S3 / Local Storage)
+        services.AddSingleton<LocalStorageService>(sp =>
+        {
+            var env = sp.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
+            var rootPath = env != null
+                ? Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads")
+                : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
+            return new LocalStorageService(rootPath);
+        });
+
+        services.AddSingleton<IFileStorageService>(sp =>
+        {
+            var config = sp.GetRequiredService<IConfiguration>();
+            var localService = sp.GetRequiredService<LocalStorageService>();
+
+            var bucketName = config["AWS:S3:BucketName"] ?? config["AWS:BucketName"] ?? config["AWS_S3_BUCKET_NAME"];
+            var accessKey = config["AWS:AccessKey"] ?? config["AWS_ACCESS_KEY_ID"];
+            var secretKey = config["AWS:SecretKey"] ?? config["AWS_SECRET_ACCESS_KEY"];
+            var region = config["AWS:Region"] ?? config["AWS_REGION"] ?? "us-east-1";
+
+            AwsS3StorageService? s3Service = null;
+            if (!string.IsNullOrWhiteSpace(bucketName))
+            {
+                Amazon.S3.IAmazonS3 s3Client;
+                if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
+                {
+                    s3Client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));
+                }
+                else
+                {
+                    s3Client = new Amazon.S3.AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(region));
+                }
+                s3Service = new AwsS3StorageService(s3Client, bucketName);
+            }
+
+            return new HybridFileStorageService(localService, s3Service);
+        });
+
 
         // Resolve JWT secret from configuration — validated at startup by IOptions<JwtOptions> in the API layer
         var jwtSecret = configuration["Jwt:SecretKey"]
diff --git a/src/Vendor.Infrastructure/Storage/AwsS3StorageService.cs b/src/Vendor.Infrastructure/Storage/AwsS3StorageService.cs
new file mode 100644
index 0000000..8782729
--- /dev/null
+++ b/src/Vendor.Infrastructure/Storage/AwsS3StorageService.cs
@@ -0,0 +1,71 @@
+using Amazon.S3;
+using Amazon.S3.Model;
+using Vendor.Application.Common.Interfaces;
+
+namespace Vendor.Infrastructure.Storage;
+
+public class AwsS3StorageService : IFileStorageService
+{
+    private readonly IAmazonS3 _s3Client;
+    private readonly string _bucketName;
+
+    public AwsS3StorageService(IAmazonS3 s3Client, string bucketName)
+    {
+        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
+        _bucketName = !string.IsNullOrWhiteSpace(bucketName) ? bucketName : throw new ArgumentNullException(nameof(bucketName));
+    }
+
+    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
+    {
+        var cleanFileName = Path.GetFileName(fileName);
+        var putRequest = new PutObjectRequest
+        {
+            BucketName = _bucketName,
+            Key = cleanFileName,
+            InputStream = stream,
+            ContentType = contentType
+        };
+
+        await _s3Client.PutObjectAsync(putRequest, ct);
+        return $"https://{_bucketName}.s3.amazonaws.com/{cleanFileName}";
+    }
+
+    public Task<string> GeneratePresignedUploadUrlAsync(string fileName, string contentType, TimeSpan expiration, CancellationToken ct = default)
+    {
+        var cleanFileName = Path.GetFileName(fileName);
+        var request = new GetPreSignedUrlRequest
+        {
+            BucketName = _bucketName,
+            Key = cleanFileName,
+            Verb = HttpVerb.PUT,
+            Expires = DateTime.UtcNow.Add(expiration),
+            ContentType = contentType
+        };
+
+        var url = _s3Client.GetPreSignedURL(request);
+        return Task.FromResult(url);
+    }
+
+    public async Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
+    {
+        if (string.IsNullOrWhiteSpace(fileUrl)) return;
+
+        var key = ExtractKeyFromUrl(fileUrl);
+        var deleteRequest = new DeleteObjectRequest
+        {
+            BucketName = _bucketName,
+            Key = key
+        };
+
+        await _s3Client.DeleteObjectAsync(deleteRequest, ct);
+    }
+
+    private static string ExtractKeyFromUrl(string fileUrl)
+    {
+        if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
+        {
+            return uri.AbsolutePath.TrimStart('/');
+        }
+        return Path.GetFileName(fileUrl);
+    }
+}
diff --git a/src/Vendor.Infrastructure/Storage/HybridFileStorageService.cs b/src/Vendor.Infrastructure/Storage/HybridFileStorageService.cs
new file mode 100644
index 0000000..7c1289b
--- /dev/null
+++ b/src/Vendor.Infrastructure/Storage/HybridFileStorageService.cs
@@ -0,0 +1,32 @@
+using Vendor.Application.Common.Interfaces;
+
+namespace Vendor.Infrastructure.Storage;
+
+public class HybridFileStorageService : IFileStorageService
+{
+    private readonly IFileStorageService _localStorageService;
+    private readonly IFileStorageService? _s3StorageService;
+
+    public HybridFileStorageService(IFileStorageService localStorageService, IFileStorageService? s3StorageService = null)
+    {
+        _localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
+        _s3StorageService = s3StorageService;
+    }
+
+    private IFileStorageService ActiveService => _s3StorageService ?? _localStorageService;
+
+    public Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
+    {
+        return ActiveService.UploadFileAsync(stream, fileName, contentType, ct);
+    }
+
+    public Task<string> GeneratePresignedUploadUrlAsync(string fileName, string contentType, TimeSpan expiration, CancellationToken ct = default)
+    {
+        return ActiveService.GeneratePresignedUploadUrlAsync(fileName, contentType, expiration, ct);
+    }
+
+    public Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
+    {
+        return ActiveService.DeleteFileAsync(fileUrl, ct);
+    }
+}
diff --git a/src/Vendor.Infrastructure/Storage/LocalStorageService.cs b/src/Vendor.Infrastructure/Storage/LocalStorageService.cs
new file mode 100644
index 0000000..b3e61d1
--- /dev/null
+++ b/src/Vendor.Infrastructure/Storage/LocalStorageService.cs
@@ -0,0 +1,58 @@
+using Vendor.Application.Common.Interfaces;
+
+namespace Vendor.Infrastructure.Storage;
+
+public class LocalStorageService : IFileStorageService
+{
+    private readonly string _rootPath;
+    private readonly string _baseUrl;
+
+    public LocalStorageService(string? rootPath = null, string baseUrl = "/uploads")
+    {
+        _rootPath = string.IsNullOrWhiteSpace(rootPath)
+            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads")
+            : rootPath;
+        _baseUrl = baseUrl;
+    }
+
+    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
+    {
+        Directory.CreateDirectory(_rootPath);
+        var cleanFileName = Path.GetFileName(fileName);
+        var filePath = Path.Combine(_rootPath, cleanFileName);
+
+        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
+        {
+            await stream.CopyToAsync(fileStream, ct);
+        }
+
+        return FormatFileUrl(cleanFileName);
+    }
+
+    public Task<string> GeneratePresignedUploadUrlAsync(string fileName, string contentType, TimeSpan expiration, CancellationToken ct = default)
+    {
+        var cleanFileName = Path.GetFileName(fileName);
+        return Task.FromResult(FormatFileUrl(cleanFileName));
+    }
+
+    public Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
+    {
+        var cleanFileName = Path.GetFileName(fileUrl);
+        if (!string.IsNullOrEmpty(cleanFileName))
+        {
+            var filePath = Path.Combine(_rootPath, cleanFileName);
+            if (File.Exists(filePath))
+            {
+                File.Delete(filePath);
+            }
+        }
+
+        return Task.CompletedTask;
+    }
+
+    private string FormatFileUrl(string fileName)
+    {
+        var prefix = string.IsNullOrWhiteSpace(_baseUrl) ? "/uploads" : _baseUrl.TrimEnd('/');
+        return $"{prefix}/{fileName}";
+    }
+}
diff --git a/src/Vendor.Infrastructure/Vendor.Infrastructure.csproj b/src/Vendor.Infrastructure/Vendor.Infrastructure.csproj
index be99257..63b0aea 100644
--- a/src/Vendor.Infrastructure/Vendor.Infrastructure.csproj
+++ b/src/Vendor.Infrastructure/Vendor.Infrastructure.csproj
@@ -24,6 +24,7 @@
     </PackageReference>
     <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
     <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="10.0.10" />
+    <PackageReference Include="AWSSDK.S3" Version="3.7.*" />
     <PackageReference Include="Stripe.net" Version="47.2.0" />
     <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.3.0" />
   </ItemGroup>
diff --git a/tests/Vendor.Api.Tests/Integration/MediaEndpointsTests.cs b/tests/Vendor.Api.Tests/Integration/MediaEndpointsTests.cs
new file mode 100644
index 0000000..80981fd
--- /dev/null
+++ b/tests/Vendor.Api.Tests/Integration/MediaEndpointsTests.cs
@@ -0,0 +1,41 @@
+using System.Net;
+using System.Net.Http.Json;
+using FluentAssertions;
+using Vendor.Api.Tests.Helpers;
+using Xunit;
+
+namespace Vendor.Api.Tests.Integration;
+
+public class MediaEndpointsTests : IClassFixture<VendorApiFactory>
+{
+    private readonly VendorApiFactory _factory;
+
+    public MediaEndpointsTests(VendorApiFactory factory)
+    {
+        _factory = factory;
+    }
+
+    [Fact]
+    public async Task GetPresignedUrl_WithValidFileName_ReturnsOkAndUrl()
+    {
+        var client = _factory.CreateClient();
+        var response = await client.GetAsync("/api/v1/media/presigned-url?fileName=avatar.png&contentType=image/png");
+
+        response.StatusCode.Should().Be(HttpStatusCode.OK);
+        var content = await response.Content.ReadFromJsonAsync<PresignedUrlResponse>();
+        content.Should().NotBeNull();
+        content!.Url.Should().NotBeNullOrEmpty();
+        content.Url.Should().Contain("avatar.png");
+    }
+
+    [Fact]
+    public async Task GetPresignedUrl_WithoutFileName_ReturnsBadRequest()
+    {
+        var client = _factory.CreateClient();
+        var response = await client.GetAsync("/api/v1/media/presigned-url");
+
+        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+    }
+
+    private record PresignedUrlResponse(string Url, string FileName);
+}
diff --git a/tests/Vendor.Infrastructure.Tests/Storage/HybridFileStorageServiceTests.cs b/tests/Vendor.Infrastructure.Tests/Storage/HybridFileStorageServiceTests.cs
new file mode 100644
index 0000000..d67feaa
--- /dev/null
+++ b/tests/Vendor.Infrastructure.Tests/Storage/HybridFileStorageServiceTests.cs
@@ -0,0 +1,103 @@
+using System.Text;
+using FluentAssertions;
+using Moq;
+using Vendor.Application.Common.Interfaces;
+using Vendor.Infrastructure.Storage;
+using Xunit;
+
+namespace Vendor.Infrastructure.Tests.Storage;
+
+public class HybridFileStorageServiceTests : IDisposable
+{
+    private readonly string _tempFolder;
+
+    public HybridFileStorageServiceTests()
+    {
+        _tempFolder = Path.Combine(Path.GetTempPath(), "vendor_tests_" + Guid.NewGuid().ToString("N"));
+        Directory.CreateDirectory(_tempFolder);
+    }
+
+    public void Dispose()
+    {
+        if (Directory.Exists(_tempFolder))
+        {
+            try
+            {
+                Directory.Delete(_tempFolder, recursive: true);
+            }
+            catch
+            {
+                // Ignore cleanup errors in test dispose
+            }
+        }
+    }
+
+    [Fact]
+    public async Task LocalStorageFallback_GeneratesValidUploadUrl()
+    {
+        var localService = new LocalStorageService(rootPath: _tempFolder);
+        var hybridService = new HybridFileStorageService(localService, s3StorageService: null);
+
+        var url = await hybridService.GeneratePresignedUploadUrlAsync("test_image.png", "image/png", TimeSpan.FromMinutes(15));
+        
+        url.Should().NotBeNullOrEmpty();
+        url.Should().Contain("test_image.png");
+    }
+
+    [Fact]
+    public async Task LocalStorage_UploadAndDeleteFile_WorksCorrectly()
+    {
+        var localService = new LocalStorageService(rootPath: _tempFolder);
+        var hybridService = new HybridFileStorageService(localService, s3StorageService: null);
+
+        var fileName = "sample.txt";
+        var content = "Hello World Storage Test";
+        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
+
+        var fileUrl = await hybridService.UploadFileAsync(stream, fileName, "text/plain");
+
+        fileUrl.Should().NotBeNullOrEmpty();
+        var filePath = Path.Combine(_tempFolder, fileName);
+        File.Exists(filePath).Should().BeTrue();
+        (await File.ReadAllTextAsync(filePath)).Should().Be(content);
+
+        await hybridService.DeleteFileAsync(fileUrl);
+        File.Exists(filePath).Should().BeFalse();
+    }
+
+    [Fact]
+    public async Task HybridStorage_UsesS3Service_WhenS3Configured()
+    {
+        var localService = new LocalStorageService(rootPath: _tempFolder);
+        var mockS3Service = new Mock<IFileStorageService>();
+        mockS3Service
+            .Setup(s => s.GeneratePresignedUploadUrlAsync("s3_file.png", "image/png", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
+            .ReturnsAsync("https://my-bucket.s3.amazonaws.com/s3_file.png?presigned=true");
+
+        var hybridService = new HybridFileStorageService(localService, mockS3Service.Object);
+
+        var url = await hybridService.GeneratePresignedUploadUrlAsync("s3_file.png", "image/png", TimeSpan.FromMinutes(15));
+
+        url.Should().Be("https://my-bucket.s3.amazonaws.com/s3_file.png?presigned=true");
+        mockS3Service.Verify(s => s.GeneratePresignedUploadUrlAsync("s3_file.png", "image/png", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
+    }
+
+    [Fact]
+    public async Task HybridStorage_UploadFile_DelegatesToS3_WhenConfigured()
+    {
+        var localService = new LocalStorageService(rootPath: _tempFolder);
+        var mockS3Service = new Mock<IFileStorageService>();
+        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
+
+        mockS3Service
+            .Setup(s => s.UploadFileAsync(stream, "test.txt", "text/plain", It.IsAny<CancellationToken>()))
+            .ReturnsAsync("https://my-bucket.s3.amazonaws.com/test.txt");
+
+        var hybridService = new HybridFileStorageService(localService, mockS3Service.Object);
+
+        var resultUrl = await hybridService.UploadFileAsync(stream, "test.txt", "text/plain");
+
+        resultUrl.Should().Be("https://my-bucket.s3.amazonaws.com/test.txt");
+        mockS3Service.Verify(s => s.UploadFileAsync(stream, "test.txt", "text/plain", It.IsAny<CancellationToken>()), Times.Once);
+    }
+}
