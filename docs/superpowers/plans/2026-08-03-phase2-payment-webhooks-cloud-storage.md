# Phase 2: Payment Webhooks & Cloud Storage Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement robust, replay-protected payment webhook ingestion for Stripe, PayMob, and PayPal with outbox event dispatching, alongside a hybrid cloud file storage service supporting AWS S3 with local filesystem fallback.

**Architecture:** Webhook endpoints authenticate incoming signature headers (`Stripe-Signature`, PayMob HMAC SHA-512, PayPal Transmission Headers) and check an EF Core `WebhookEvents` database table for replay protection. The file storage subsystem exposes `IFileStorageService` backing AWS S3 or local `wwwroot/uploads` with presigned URL generation capabilities.

**Tech Stack:** .NET 9, EF Core, Stripe.net, AWSSDK.S3, MediatR, FluentAssertions, xUnit.

## Global Constraints

- Solution file: `Vendor.slnx`
- Target framework: `net9.0`
- Zero build warnings or broken tests (`dotnet test Vendor.slnx` must pass 100%)
- Exact file paths and types must match Clean Architecture boundaries

---

### Task 1: Webhook Replay Protection Entity & Persistence

**Files:**
- Create: `src/Vendor.Domain/Entities/WebhookEvent.cs`
- Create: `src/Vendor.Domain/Interfaces/Repositories/IWebhookEventRepository.cs`
- Create: `src/Vendor.Infrastructure/Persistence/Configurations/WebhookEventConfiguration.cs`
- Modify: `src/Vendor.Infrastructure/Persistence/VendorDbContext.cs`
- Modify: `src/Vendor.Infrastructure/Persistence/Repositories/Repositories.cs`
- Modify: `src/Vendor.Infrastructure/DependencyInjection.cs`
- Create: `tests/Vendor.Infrastructure.Tests/Persistence/WebhookEventRepositoryTests.cs`

**Interfaces:**
- Consumes: `VendorDbContext`
- Produces: `IWebhookEventRepository` (`ExistsAsync`, `AddAsync`)

- [ ] **Step 1: Write failing unit test for WebhookEventRepository**

Create `tests/Vendor.Infrastructure.Tests/Persistence/WebhookEventRepositoryTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Vendor.Domain.Entities;
using Vendor.Infrastructure.Persistence;
using Vendor.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Vendor.Infrastructure.Tests.Persistence;

public class WebhookEventRepositoryTests
{
    private static VendorDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new VendorDbContext(options);
    }

    [Fact]
    public async Task AddAsync_And_ExistsAsync_Works_Correctly()
    {
        using var context = CreateInMemoryDbContext();
        var repo = new WebhookEventRepository(context);

        var provider = "Stripe";
        var eventId = "evt_test_12345";
        var webhookEvent = new WebhookEvent(Guid.NewGuid(), provider, eventId, "payment_intent.succeeded", "{}");

        var existsBefore = await repo.ExistsAsync(provider, eventId);
        Assert.False(existsBefore);

        await repo.AddAsync(webhookEvent);

        var existsAfter = await repo.ExistsAsync(provider, eventId);
        Assert.True(existsAfter);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Vendor.Infrastructure.Tests --filter "FullyQualifiedName~WebhookEventRepositoryTests"`
Expected: FAIL (`WebhookEvent` / `WebhookEventRepository` missing).

- [ ] **Step 3: Create WebhookEvent entity and WebhookEventRepository**

Create `src/Vendor.Domain/Entities/WebhookEvent.cs`:
```csharp
namespace Vendor.Domain.Entities;

public class WebhookEvent
{
    public Guid Id { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string EventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; private set; }

    private WebhookEvent() { }

    public WebhookEvent(Guid id, string provider, string eventId, string eventType, string payloadJson)
    {
        Id = id;
        Provider = provider;
        EventId = eventId;
        EventType = eventType;
        PayloadJson = payloadJson;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}
```

Create `src/Vendor.Domain/Interfaces/Repositories/IWebhookEventRepository.cs`:
```csharp
using Vendor.Domain.Entities;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IWebhookEventRepository
{
    Task<bool> ExistsAsync(string provider, string eventId, CancellationToken ct = default);
    Task AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default);
}
```

Create EF Core configuration `src/Vendor.Infrastructure/Persistence/Configurations/WebhookEventConfiguration.cs`, update `VendorDbContext.cs`, implement `WebhookEventRepository`, and register in `DependencyInjection.cs`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Vendor.Infrastructure.Tests --filter "FullyQualifiedName~WebhookEventRepositoryTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(webhooks): add WebhookEvent entity and repository for replay protection"
```

---

### Task 2: Payment Webhooks Signature Verification & Endpoints

**Files:**
- Create: `src/Vendor.Infrastructure/Payments/Webhooks/StripeWebhookParser.cs`
- Create: `src/Vendor.Infrastructure/Payments/Webhooks/PaymobWebhookParser.cs`
- Create: `src/Vendor.Infrastructure/Payments/Webhooks/PaypalWebhookParser.cs`
- Create: `src/Vendor.Application/Modules/Payments/ProcessPaymentWebhookCommand.cs`
- Create: `src/Vendor.Api/Endpoints/WebhookEndpoints.cs`
- Modify: `src/Vendor.Api/Program.cs`
- Create: `tests/Vendor.Api.Tests/Integration/WebhookEndpointsTests.cs`

**Interfaces:**
- Consumes: `IWebhookEventRepository`, `IPublisher`, `Stripe.EventUtility`
- Produces: `POST /api/v1/webhooks/stripe`, `POST /api/v1/webhooks/paymob`, `POST /api/v1/webhooks/paypal`

- [ ] **Step 1: Write failing integration test for WebhookEndpoints**

Create `tests/Vendor.Api.Tests/Integration/WebhookEndpointsTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Vendor.Api.Tests.Helpers;
using Xunit;

namespace Vendor.Api.Tests.Integration;

public class WebhookEndpointsTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public WebhookEndpointsTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StripeWebhook_WithInvalidSignature_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Stripe-Signature", "t=123,v1=invalid_sig");

        var response = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Vendor.Api.Tests --filter "FullyQualifiedName~WebhookEndpointsTests"`
Expected: FAIL (404 Not Found).

- [ ] **Step 3: Implement Webhook Parsers, Handler, and Endpoints**

Create `StripeWebhookParser`, `PaymobWebhookParser`, `PaypalWebhookParser` in Infrastructure.
Create `ProcessPaymentWebhookCommandHandler` in Application.
Create `src/Vendor.Api/Endpoints/WebhookEndpoints.cs`:
```csharp
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Application.Modules.Payments;

namespace Vendor.Api.Endpoints;

public static class WebhookEndpoints
{
    public static RouteGroupBuilder MapWebhookEndpoints(this RouteGroupBuilder group)
    {
        var webhooks = group.MapGroup("/webhooks").WithTags("Webhooks");

        webhooks.MapPost("/stripe", async (HttpRequest request, ISender mediator, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var sigHeader = request.Headers["Stripe-Signature"].ToString();

            if (string.IsNullOrEmpty(sigHeader) || string.IsNullOrEmpty(rawBody))
            {
                return Results.BadRequest(new { Error = "Invalid Stripe webhook payload or signature." });
            }

            var command = new ProcessPaymentWebhookCommand("Stripe", sigHeader, rawBody);
            var result = await mediator.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Error = result.Error.Description });
        });

        webhooks.MapPost("/paymob", async (HttpRequest request, ISender mediator, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var hmacHeader = request.Headers["Paymob-HMAC"].ToString();

            var command = new ProcessPaymentWebhookCommand("PayMob", hmacHeader, rawBody);
            var result = await mediator.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Error = result.Error.Description });
        });

        webhooks.MapPost("/paypal", async (HttpRequest request, ISender mediator, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var transmissionId = request.Headers["PAYPAL-TRANSMISSION-ID"].ToString();

            var command = new ProcessPaymentWebhookCommand("PayPal", transmissionId, rawBody);
            var result = await mediator.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Error = result.Error.Description });
        });

        return group;
    }
}
```

Wire `app.MapGroup("/api/v1").MapWebhookEndpoints();` in `Program.cs`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Vendor.Api.Tests --filter "FullyQualifiedName~WebhookEndpointsTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(webhooks): implement Stripe, PayMob, and PayPal webhook endpoints with signature validation"
```

---

### Task 3: Cloud File Storage Service (`IFileStorageService`)

**Files:**
- Create: `src/Vendor.Application/Common/Interfaces/IFileStorageService.cs`
- Create: `src/Vendor.Infrastructure/Storage/AwsS3StorageService.cs`
- Create: `src/Vendor.Infrastructure/Storage/LocalStorageService.cs`
- Create: `src/Vendor.Infrastructure/Storage/HybridFileStorageService.cs`
- Modify: `src/Vendor.Infrastructure/DependencyInjection.cs`
- Create: `src/Vendor.Api/Endpoints/MediaEndpoints.cs`
- Create: `tests/Vendor.Infrastructure.Tests/Storage/HybridFileStorageServiceTests.cs`

**Interfaces:**
- Consumes: `IConfiguration`, `IWebHostEnvironment`
- Produces: `IFileStorageService`, `GET /api/v1/media/presigned-url`

- [ ] **Step 1: Write failing unit test for HybridFileStorageService**

Create `tests/Vendor.Infrastructure.Tests/Storage/HybridFileStorageServiceTests.cs`:
```csharp
using Vendor.Infrastructure.Storage;
using Xunit;

namespace Vendor.Infrastructure.Tests.Storage;

public class HybridFileStorageServiceTests
{
    [Fact]
    public async Task LocalStorageFallback_GeneratesValidUploadUrl()
    {
        var localService = new LocalStorageService(rootPath: Path.GetTempPath());
        var hybridService = new HybridFileStorageService(localService, s3Service: null);

        var url = await hybridService.GeneratePresignedUploadUrlAsync("test_image.png", "image/png", TimeSpan.FromMinutes(15));
        Assert.NotNull(url);
        Assert.Contains("test_image.png", url);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Vendor.Infrastructure.Tests --filter "FullyQualifiedName~HybridFileStorageServiceTests"`
Expected: FAIL (`IFileStorageService` missing).

- [ ] **Step 3: Create IFileStorageService and HybridFileStorageService**

Create `src/Vendor.Application/Common/Interfaces/IFileStorageService.cs`:
```csharp
namespace Vendor.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
    Task<string> GeneratePresignedUploadUrlAsync(string fileName, string contentType, TimeSpan expiration, CancellationToken ct = default);
    Task DeleteFileAsync(string fileUrl, CancellationToken ct = default);
}
```

Create `LocalStorageService.cs`, `AwsS3StorageService.cs`, and `HybridFileStorageService.cs`.
Register `IFileStorageService` as `Singleton` in `src/Vendor.Infrastructure/DependencyInjection.cs`.
Create `src/Vendor.Api/Endpoints/MediaEndpoints.cs` for presigned URL requests.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Vendor.Infrastructure.Tests --filter "FullyQualifiedName~HybridFileStorageServiceTests"`
Expected: PASS.

- [ ] **Step 5: Run full test suite and Commit**

Run: `dotnet test Vendor.slnx`
Expected: ALL tests pass.

```bash
git add .
git commit -m "feat(storage): implement HybridFileStorageService supporting AWS S3 with LocalStorage fallback"
```
