diff --git a/.superpowers/sdd/2026-08-03-phase2-payment-webhooks-cloud-storage/task-1-report.md b/.superpowers/sdd/2026-08-03-phase2-payment-webhooks-cloud-storage/task-1-report.md
new file mode 100644
index 0000000..28511b1
--- /dev/null
+++ b/.superpowers/sdd/2026-08-03-phase2-payment-webhooks-cloud-storage/task-1-report.md
@@ -0,0 +1,26 @@
+# Task 1 Report: Webhook Replay Protection Entity & Persistence
+
+**Status:** DONE  
+**Date:** 2026-08-03  
+
+## Summary
+Successfully implemented the `WebhookEvent` domain entity, `IWebhookEventRepository` interface, EF Core entity configuration, DbContext integration, and repository implementation to support replay protection for incoming payment webhooks (Stripe, PayMob, PayPal).
+
+## Changes Made
+1. **Domain Entity (`src/Vendor.Domain/Entities/WebhookEvent.cs`)**:
+   - Created `WebhookEvent` with properties: `Id`, `Provider`, `EventId`, `EventType`, `PayloadJson`, `ProcessedAtUtc`.
+2. **Repository Interface (`src/Vendor.Domain/Interfaces/Repositories/IWebhookEventRepository.cs`)**:
+   - Added `ExistsAsync(string provider, string eventId, CancellationToken ct)` and `AddAsync(WebhookEvent webhookEvent, CancellationToken ct)`.
+3. **EF Core Configuration (`src/Vendor.Infrastructure/Persistence/Configurations/WebhookEventConfiguration.cs`)**:
+   - Mapped table `WebhookEvents` with unique index on composite key `(Provider, EventId)`.
+4. **DbContext (`src/Vendor.Infrastructure/Persistence/VendorDbContext.cs`)**:
+   - Added `DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();`.
+5. **Repository Implementation (`src/Vendor.Infrastructure/Persistence/Repositories/WebhookEventRepository.cs`)**:
+   - Implemented `ExistsAsync` and `AddAsync(WebhookEvent)` with automatic `SaveChangesAsync` persistence.
+   - Kept registered as Scoped in `DependencyInjection.cs`.
+6. **Unit Tests (`tests/Vendor.Infrastructure.Tests/Persistence/WebhookEventRepositoryTests.cs`)**:
+   - Created xUnit test verifying `ExistsAsync` returns false prior to insertion and true post insertion.
+
+## Verification
+- `dotnet test tests/Vendor.Infrastructure.Tests --filter "FullyQualifiedName~WebhookEventRepositoryTests"` PASSED (1/1).
+- `dotnet test Vendor.slnx` PASSED (196/196 tests passed across all projects).
diff --git a/src/Vendor.Domain/Entities/WebhookEvent.cs b/src/Vendor.Domain/Entities/WebhookEvent.cs
new file mode 100644
index 0000000..88fd55d
--- /dev/null
+++ b/src/Vendor.Domain/Entities/WebhookEvent.cs
@@ -0,0 +1,23 @@
+namespace Vendor.Domain.Entities;
+
+public class WebhookEvent
+{
+    public Guid Id { get; private set; }
+    public string Provider { get; private set; } = string.Empty;
+    public string EventId { get; private set; } = string.Empty;
+    public string EventType { get; private set; } = string.Empty;
+    public string PayloadJson { get; private set; } = string.Empty;
+    public DateTime ProcessedAtUtc { get; private set; }
+
+    private WebhookEvent() { }
+
+    public WebhookEvent(Guid id, string provider, string eventId, string eventType, string payloadJson)
+    {
+        Id = id;
+        Provider = provider;
+        EventId = eventId;
+        EventType = eventType;
+        PayloadJson = payloadJson;
+        ProcessedAtUtc = DateTime.UtcNow;
+    }
+}
diff --git a/src/Vendor.Domain/Interfaces/Repositories/IWebhookEventRepository.cs b/src/Vendor.Domain/Interfaces/Repositories/IWebhookEventRepository.cs
index dbf7689..c2aa7ad 100644
--- a/src/Vendor.Domain/Interfaces/Repositories/IWebhookEventRepository.cs
+++ b/src/Vendor.Domain/Interfaces/Repositories/IWebhookEventRepository.cs
@@ -1,9 +1,14 @@
 using Vendor.Domain.Aggregates.Payment;
+using Vendor.Domain.Entities;
 
 namespace Vendor.Domain.Interfaces.Repositories;
 
 public interface IWebhookEventRepository
 {
+    Task<bool> ExistsAsync(string provider, string eventId, CancellationToken ct = default);
+    Task AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default);
+
     Task<WebhookEventEntry?> GetByGatewayAndEventIdAsync(string gatewayName, string eventId, CancellationToken ct = default);
     Task AddAsync(WebhookEventEntry webhookEvent, CancellationToken ct = default);
 }
+
diff --git a/src/Vendor.Infrastructure/Persistence/Configurations/WebhookEventConfiguration.cs b/src/Vendor.Infrastructure/Persistence/Configurations/WebhookEventConfiguration.cs
new file mode 100644
index 0000000..c77402d
--- /dev/null
+++ b/src/Vendor.Infrastructure/Persistence/Configurations/WebhookEventConfiguration.cs
@@ -0,0 +1,36 @@
+using Microsoft.EntityFrameworkCore;
+using Microsoft.EntityFrameworkCore.Metadata.Builders;
+using Vendor.Domain.Entities;
+
+namespace Vendor.Infrastructure.Persistence.Configurations;
+
+public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
+{
+    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
+    {
+        builder.ToTable("WebhookEvents");
+
+        builder.HasKey(x => x.Id);
+
+        builder.Property(x => x.Provider)
+            .IsRequired()
+            .HasMaxLength(64);
+
+        builder.Property(x => x.EventId)
+            .IsRequired()
+            .HasMaxLength(128);
+
+        builder.HasIndex(x => new { x.Provider, x.EventId })
+            .IsUnique();
+
+        builder.Property(x => x.EventType)
+            .IsRequired()
+            .HasMaxLength(128);
+
+        builder.Property(x => x.PayloadJson)
+            .IsRequired();
+
+        builder.Property(x => x.ProcessedAtUtc)
+            .IsRequired();
+    }
+}
diff --git a/src/Vendor.Infrastructure/Persistence/Repositories/WebhookEventRepository.cs b/src/Vendor.Infrastructure/Persistence/Repositories/WebhookEventRepository.cs
index 2e41558..e46fdd4 100644
--- a/src/Vendor.Infrastructure/Persistence/Repositories/WebhookEventRepository.cs
+++ b/src/Vendor.Infrastructure/Persistence/Repositories/WebhookEventRepository.cs
@@ -1,11 +1,24 @@
 using Microsoft.EntityFrameworkCore;
 using Vendor.Domain.Aggregates.Payment;
+using Vendor.Domain.Entities;
 using Vendor.Domain.Interfaces.Repositories;
 
 namespace Vendor.Infrastructure.Persistence.Repositories;
 
 public class WebhookEventRepository(VendorDbContext context) : IWebhookEventRepository
 {
+    public async Task<bool> ExistsAsync(string provider, string eventId, CancellationToken ct = default)
+    {
+        return await context.WebhookEvents
+            .AnyAsync(w => w.Provider == provider && w.EventId == eventId, ct);
+    }
+
+    public async Task AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default)
+    {
+        await context.WebhookEvents.AddAsync(webhookEvent, ct);
+        await context.SaveChangesAsync(ct);
+    }
+
     public async Task<WebhookEventEntry?> GetByGatewayAndEventIdAsync(string gatewayName, string eventId, CancellationToken ct = default)
     {
         return await context.WebhookEventEntries
diff --git a/src/Vendor.Infrastructure/Persistence/VendorDbContext.cs b/src/Vendor.Infrastructure/Persistence/VendorDbContext.cs
index 8aa2f6c..555d097 100644
--- a/src/Vendor.Infrastructure/Persistence/VendorDbContext.cs
+++ b/src/Vendor.Infrastructure/Persistence/VendorDbContext.cs
@@ -10,6 +10,7 @@ using Vendor.Domain.Aggregates.Product;
 using Vendor.Domain.Aggregates.Promotion;
 using Vendor.Domain.Aggregates.ReturnRequest;
 using Vendor.Domain.Aggregates.Shipment;
+using Vendor.Domain.Entities;
 using Vendor.Infrastructure.Auth;
 using Vendor.Infrastructure.Identity;
 using Vendor.Infrastructure.Outbox;
@@ -33,6 +34,7 @@ public class VendorDbContext(DbContextOptions<VendorDbContext> options) : Identi
     public DbSet<PaymentIdempotencyKey> PaymentIdempotencyKeys => Set<PaymentIdempotencyKey>();
     public DbSet<PaymentLedgerEntry> PaymentLedgerEntries => Set<PaymentLedgerEntry>();
     public DbSet<WebhookEventEntry> WebhookEventEntries => Set<WebhookEventEntry>();
+    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
     public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
     public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
 
diff --git a/tests/Vendor.Infrastructure.Tests/Persistence/WebhookEventRepositoryTests.cs b/tests/Vendor.Infrastructure.Tests/Persistence/WebhookEventRepositoryTests.cs
new file mode 100644
index 0000000..775a565
--- /dev/null
+++ b/tests/Vendor.Infrastructure.Tests/Persistence/WebhookEventRepositoryTests.cs
@@ -0,0 +1,37 @@
+using Microsoft.EntityFrameworkCore;
+using Vendor.Domain.Entities;
+using Vendor.Infrastructure.Persistence;
+using Vendor.Infrastructure.Persistence.Repositories;
+using Xunit;
+
+namespace Vendor.Infrastructure.Tests.Persistence;
+
+public class WebhookEventRepositoryTests
+{
+    private static VendorDbContext CreateInMemoryDbContext()
+    {
+        var options = new DbContextOptionsBuilder<VendorDbContext>()
+            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
+            .Options;
+        return new VendorDbContext(options);
+    }
+
+    [Fact]
+    public async Task AddAsync_And_ExistsAsync_Works_Correctly()
+    {
+        using var context = CreateInMemoryDbContext();
+        var repo = new WebhookEventRepository(context);
+
+        var provider = "Stripe";
+        var eventId = "evt_test_12345";
+        var webhookEvent = new WebhookEvent(Guid.NewGuid(), provider, eventId, "payment_intent.succeeded", "{}");
+
+        var existsBefore = await repo.ExistsAsync(provider, eventId);
+        Assert.False(existsBefore);
+
+        await repo.AddAsync(webhookEvent);
+
+        var existsAfter = await repo.ExistsAsync(provider, eventId);
+        Assert.True(existsAfter);
+    }
+}
