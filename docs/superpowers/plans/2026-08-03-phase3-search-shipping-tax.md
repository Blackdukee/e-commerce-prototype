# Phase 3 — Search Engine & Live Shipping/Tax APIs — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace product search, shipping, and tax stubs with Elasticsearch, Shippo, and TaxJar live adapters — each with config-gated fallback to existing stubs.

**Architecture:** Three independent adapters behind Application/Domain interfaces; DI selects the live adapter when its config key is present, otherwise falls back to the existing stub. A Hangfire recurring job syncs the Elasticsearch product index every 5 minutes; a domain event handler syncs immediately on product activation.

**Tech Stack:** Elastic.Clients.Elasticsearch 8.x, Shippo REST API (System.Text.Json), TaxJar REST API (System.Text.Json), Hangfire (already wired), EF Core 9 (already wired), xUnit + Moq + FluentAssertions (already in test projects).

## Global Constraints

- Target framework: net9.0 across all projects.
- Clean Architecture: interfaces in Vendor.Application or Vendor.Domain; adapters in Vendor.Infrastructure; endpoints in Vendor.Api.
- No breaking changes to IShippingProvider, ITaxCalculator, or any existing interface.
- `PagedResult<T>` is already defined at `src/Vendor.Application/Modules/Customers/Queries/AccountManagementQueries.cs` as `record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageIndex, int PageSize)` — reuse it, do NOT redefine.
- All HTTP adapters use IHttpClientFactory typed clients; no raw HttpClient construction.
- All tests: xUnit, Moq, FluentAssertions. Full suite: `dotnet test Vendor.slnx`
- Commit after every task with a conventional commit message.

---

## Task 1: IProductSearchService Interface + Models

**Files:**
- Create: `src/Vendor.Application/Common/Interfaces/IProductSearchService.cs`
- Create: `src/Vendor.Application/Common/Models/ProductSearchDoc.cs`
- Create: `src/Vendor.Application/Common/Models/ProductSearchFilters.cs`
- Test: `tests/Vendor.Application.Tests/Search/ProductSearchModelsTests.cs`

**Interfaces:**
- Produces:
  - `IProductSearchService` with three methods (see Step 2)
  - `ProductSearchDoc` record: `Id`(string), `Name`(string), `Slug`(string), `Description`(string?), `BasePrice`(decimal), `Currency`(string), `Status`(string), `CreatedAtUtc`(DateTime)
  - `ProductSearchFilters` record: `MinPrice`(decimal?), `MaxPrice`(decimal?), `Status`(string? = "Active")

- [ ] **Step 1: Create ProductSearchDoc and ProductSearchFilters**

`src/Vendor.Application/Common/Models/ProductSearchDoc.cs`:
```csharp
namespace Vendor.Application.Common.Models;

public record ProductSearchDoc(
    string Id,
    string Name,
    string Slug,
    string? Description,
    decimal BasePrice,
    string Currency,
    string Status,
    DateTime CreatedAtUtc);
```

`src/Vendor.Application/Common/Models/ProductSearchFilters.cs`:
```csharp
namespace Vendor.Application.Common.Models;

public record ProductSearchFilters(
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Status = "Active");
```

- [ ] **Step 2: Create IProductSearchService**

`src/Vendor.Application/Common/Interfaces/IProductSearchService.cs`:
```csharp
using Vendor.Application.Common.Models;
using Vendor.Application.Modules.Customers.Queries;

namespace Vendor.Application.Common.Interfaces;

public interface IProductSearchService
{
    Task<PagedResult<ProductSearchDoc>> SearchProductsAsync(
        string? query,
        ProductSearchFilters filters,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task IndexProductAsync(ProductSearchDoc doc, CancellationToken ct = default);
    Task DeleteProductIndexAsync(string productId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Write compilation test**

`tests/Vendor.Application.Tests/Search/ProductSearchModelsTests.cs`:
```csharp
using FluentAssertions;
using Vendor.Application.Common.Models;
using Xunit;

namespace Vendor.Application.Tests.Search;

public class ProductSearchModelsTests
{
    [Fact]
    public void ProductSearchDoc_CanBeConstructed()
    {
        var doc = new ProductSearchDoc("p1", "Shoe", "shoe", "Nice shoe", 49.99m, "USD", "Active", DateTime.UtcNow);
        doc.Id.Should().Be("p1");
        doc.BasePrice.Should().Be(49.99m);
    }

    [Fact]
    public void ProductSearchFilters_DefaultStatus_IsActive()
    {
        var filters = new ProductSearchFilters();
        filters.Status.Should().Be("Active");
        filters.MinPrice.Should().BeNull();
    }
}
```

- [ ] **Step 4: Run tests**

```
dotnet test tests/Vendor.Application.Tests/Vendor.Application.Tests.csproj -v normal
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```
git add src/Vendor.Application/Common/Interfaces/IProductSearchService.cs src/Vendor.Application/Common/Models/ tests/Vendor.Application.Tests/Search/
git commit -m "feat(search): add IProductSearchService interface and search models"
```

---

## Task 2: EF Core Fallback Search Service

**Files:**
- Create: `src/Vendor.Infrastructure/Search/EfCoreProductSearchService.cs`
- Test: `tests/Vendor.Infrastructure.Tests/Search/EfCoreProductSearchServiceTests.cs`

**Interfaces:**
- Consumes: `IProductSearchService` (Task 1), `VendorDbContext.Products` (DbSet<Product>), `ProductSearchDoc`, `ProductSearchFilters`, `PagedResult<T>`
- Produces: `EfCoreProductSearchService : IProductSearchService`

Note: `Product` aggregate — `Name`(string), `Slug.Value`(string), `Description`(string?), `BasePrice.Amount`(decimal), `BasePrice.Currency`(string), `Status`(ProductStatus enum), `CreatedAtUtc`(DateTime), `Id.Value`(Guid). `ProductStatus.Active` is the active status enum value.

- [ ] **Step 1: Write failing tests**

`tests/Vendor.Infrastructure.Tests/Search/EfCoreProductSearchServiceTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendor.Application.Common.Models;
using Vendor.Infrastructure.Persistence;
using Vendor.Infrastructure.Search;
using Xunit;

namespace Vendor.Infrastructure.Tests.Search;

public class EfCoreProductSearchServiceTests
{
    private VendorDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new VendorDbContext(options);
    }

    [Fact]
    public async Task SearchProductsAsync_WithEmptyDb_ReturnsEmpty()
    {
        await using var ctx = CreateInMemoryContext();
        var svc = new EfCoreProductSearchService(ctx);
        var result = await svc.SearchProductsAsync(null, new ProductSearchFilters(), 1, 20);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task IndexProductAsync_IsNoOp_DoesNotThrow()
    {
        await using var ctx = CreateInMemoryContext();
        var svc = new EfCoreProductSearchService(ctx);
        var doc = new ProductSearchDoc("p1", "Shoe", "shoe", null, 49.99m, "USD", "Active", DateTime.UtcNow);
        var act = async () => await svc.IndexProductAsync(doc);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteProductIndexAsync_IsNoOp_DoesNotThrow()
    {
        await using var ctx = CreateInMemoryContext();
        var svc = new EfCoreProductSearchService(ctx);
        var act = async () => await svc.DeleteProductIndexAsync("p1");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SearchProductsAsync_ReturnsCorrectPageMetadata()
    {
        await using var ctx = CreateInMemoryContext();
        var svc = new EfCoreProductSearchService(ctx);
        var result = await svc.SearchProductsAsync(null, new ProductSearchFilters(), 1, 5);
        result.PageSize.Should().Be(5);
        result.PageIndex.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run to confirm FAIL (compilation error expected)**

```
dotnet test tests/Vendor.Infrastructure.Tests/Vendor.Infrastructure.Tests.csproj --filter "EfCoreProductSearch" -v normal
```

- [ ] **Step 3: Implement EfCoreProductSearchService**

`src/Vendor.Infrastructure/Search/EfCoreProductSearchService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Application.Modules.Customers.Queries;
using Vendor.Domain.Aggregates.Product;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Search;

public class EfCoreProductSearchService(VendorDbContext dbContext) : IProductSearchService
{
    public async Task<PagedResult<ProductSearchDoc>> SearchProductsAsync(
        string? query,
        ProductSearchFilters filters,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = dbContext.Products.AsNoTracking();

        var statusFilter = filters.Status ?? "Active";
        if (Enum.TryParse<ProductStatus>(statusFilter, ignoreCase: true, out var status))
            q = q.Where(p => p.Status == status);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query}%";
            q = q.Where(p =>
                EF.Functions.Like(p.Name, pattern) ||
                EF.Functions.Like(p.Description ?? "", pattern));
        }

        if (filters.MinPrice.HasValue)
            q = q.Where(p => p.BasePrice.Amount >= filters.MinPrice.Value);

        if (filters.MaxPrice.HasValue)
            q = q.Where(p => p.BasePrice.Amount <= filters.MaxPrice.Value);

        var totalCount = await q.CountAsync(ct);
        var skip = (page - 1) * pageSize;

        var items = await q
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .Select(p => new ProductSearchDoc(
                p.Id.Value.ToString(),
                p.Name,
                p.Slug.Value,
                p.Description,
                p.BasePrice.Amount,
                p.BasePrice.Currency,
                p.Status.ToString(),
                p.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<ProductSearchDoc>(items, totalCount, page, pageSize);
    }

    public Task IndexProductAsync(ProductSearchDoc doc, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteProductIndexAsync(string productId, CancellationToken ct = default) => Task.CompletedTask;
}
```

- [ ] **Step 4: Run tests**

```
dotnet test tests/Vendor.Infrastructure.Tests/Vendor.Infrastructure.Tests.csproj --filter "EfCoreProductSearch" -v normal
```

Expected: all 4 pass.

- [ ] **Step 5: Commit**

```
git add src/Vendor.Infrastructure/Search/EfCoreProductSearchService.cs tests/Vendor.Infrastructure.Tests/Search/EfCoreProductSearchServiceTests.cs
git commit -m "feat(search): implement EfCoreProductSearchService as fallback adapter"
```

---

## Task 3: Elasticsearch Adapter + Hybrid Selector + DI

**Files:**
- Create: `src/Vendor.Infrastructure/Search/ElasticsearchProductSearchService.cs`
- Create: `src/Vendor.Infrastructure/Search/HybridProductSearchService.cs`
- Modify: `src/Vendor.Infrastructure/DependencyInjection.cs`
- Modify: `src/Vendor.Infrastructure/Vendor.Infrastructure.csproj`
- Test: `tests/Vendor.Infrastructure.Tests/Search/ElasticsearchProductSearchServiceTests.cs`
- Test: `tests/Vendor.Infrastructure.Tests/Search/HybridProductSearchServiceTests.cs`

**Interfaces:**
- Consumes: `IProductSearchService` (Task 1), `ProductSearchDoc`, `ProductSearchFilters`, `PagedResult<T>`, `EfCoreProductSearchService` (Task 2)
- Produces: `ElasticsearchProductSearchService : IProductSearchService`, `HybridProductSearchService : IProductSearchService`

- [ ] **Step 1: Add NuGet package**

```
dotnet add src/Vendor.Infrastructure/Vendor.Infrastructure.csproj package Elastic.Clients.Elasticsearch --version 8.*
```

- [ ] **Step 2: Write failing tests**

`tests/Vendor.Infrastructure.Tests/Search/ElasticsearchProductSearchServiceTests.cs`:
```csharp
using Elastic.Clients.Elasticsearch;
using FluentAssertions;
using Vendor.Infrastructure.Search;
using Xunit;

namespace Vendor.Infrastructure.Tests.Search;

public class ElasticsearchProductSearchServiceTests
{
    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        var act = () => new ElasticsearchProductSearchService(null!, "products");
        act.Should().Throw<ArgumentNullException>().WithParameterName("client");
    }

    [Fact]
    public void Constructor_WithNullIndexName_ThrowsArgumentNullException()
    {
        var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var client = new ElasticsearchClient(settings);
        var act = () => new ElasticsearchProductSearchService(client, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("indexName");
    }
}
```

`tests/Vendor.Infrastructure.Tests/Search/HybridProductSearchServiceTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Application.Modules.Customers.Queries;
using Vendor.Infrastructure.Persistence;
using Vendor.Infrastructure.Search;
using Xunit;

namespace Vendor.Infrastructure.Tests.Search;

public class HybridProductSearchServiceTests
{
    private VendorDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new VendorDbContext(options);
    }

    [Fact]
    public async Task WhenElasticsearchNotConfigured_UsesEfCoreFallback()
    {
        await using var ctx = CreateInMemoryContext();
        var efService = new EfCoreProductSearchService(ctx);
        var hybrid = new HybridProductSearchService(efService, elasticsearchService: null);
        var result = await hybrid.SearchProductsAsync(null, new ProductSearchFilters(), 1, 20);
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenElasticsearchProvided_DelegatesSearchToIt()
    {
        var mockEs = new Mock<IProductSearchService>();
        mockEs.Setup(s => s.SearchProductsAsync(It.IsAny<string?>(), It.IsAny<ProductSearchFilters>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new PagedResult<ProductSearchDoc>([], 0, 1, 20));
        await using var ctx = CreateInMemoryContext();
        var efService = new EfCoreProductSearchService(ctx);
        var hybrid = new HybridProductSearchService(efService, mockEs.Object);

        await hybrid.SearchProductsAsync(null, new ProductSearchFilters(), 1, 20);

        mockEs.Verify(s => s.SearchProductsAsync(null, It.IsAny<ProductSearchFilters>(), 1, 20, default), Times.Once);
    }

    [Fact]
    public async Task IndexProductAsync_WhenEsConfigured_DelegatesToEs()
    {
        var mockEs = new Mock<IProductSearchService>();
        mockEs.Setup(s => s.IndexProductAsync(It.IsAny<ProductSearchDoc>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        await using var ctx = CreateInMemoryContext();
        var efService = new EfCoreProductSearchService(ctx);
        var hybrid = new HybridProductSearchService(efService, mockEs.Object);
        var doc = new ProductSearchDoc("p1", "Shoe", "shoe", null, 49.99m, "USD", "Active", DateTime.UtcNow);

        await hybrid.IndexProductAsync(doc);

        mockEs.Verify(s => s.IndexProductAsync(doc, default), Times.Once);
    }
}
```

- [ ] **Step 3: Run to confirm FAIL**

```
dotnet test tests/Vendor.Infrastructure.Tests/Vendor.Infrastructure.Tests.csproj --filter "ElasticsearchProductSearch|HybridProductSearch" -v normal
```

- [ ] **Step 4: Implement ElasticsearchProductSearchService**

`src/Vendor.Infrastructure/Search/ElasticsearchProductSearchService.cs`:
```csharp
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Application.Modules.Customers.Queries;

namespace Vendor.Infrastructure.Search;

public class ElasticsearchProductSearchService : IProductSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly string _indexName;

    public ElasticsearchProductSearchService(ElasticsearchClient client, string indexName)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _indexName = !string.IsNullOrWhiteSpace(indexName)
            ? indexName
            : throw new ArgumentNullException(nameof(indexName));
    }

    public async Task<PagedResult<ProductSearchDoc>> SearchProductsAsync(
        string? query, ProductSearchFilters filters, int page, int pageSize, CancellationToken ct = default)
    {
        var from = (page - 1) * pageSize;
        var mustClauses = new List<Query>();

        // Status filter (term)
        mustClauses.Add(Query.Term(t => t.Field("status").Value(filters.Status ?? "Active")));

        // Full-text multi-match
        if (!string.IsNullOrWhiteSpace(query))
            mustClauses.Add(Query.MultiMatch(mm => mm.Fields(new[] { "name", "description" }).Query(query)));

        // Price range
        if (filters.MinPrice.HasValue || filters.MaxPrice.HasValue)
        {
            mustClauses.Add(Query.Range(r => r.NumberRange(nr =>
            {
                nr.Field("basePrice");
                if (filters.MinPrice.HasValue) nr.Gte((double)filters.MinPrice.Value);
                if (filters.MaxPrice.HasValue) nr.Lte((double)filters.MaxPrice.Value);
            })));
        }

        var boolQuery = mustClauses.Count == 1
            ? mustClauses[0]
            : Query.Bool(b => b.Must(mustClauses.ToArray()));

        var response = await _client.SearchAsync<ProductSearchDoc>(s => s
            .Index(_indexName)
            .From(from)
            .Size(pageSize)
            .Query(boolQuery), ct);

        if (!response.IsValidResponse)
            return new PagedResult<ProductSearchDoc>([], 0, page, pageSize);

        var total = (int)(response.Total ?? 0);
        return new PagedResult<ProductSearchDoc>(response.Documents.ToList(), total, page, pageSize);
    }

    public async Task IndexProductAsync(ProductSearchDoc doc, CancellationToken ct = default)
        => await _client.IndexAsync(doc, i => i.Index(_indexName).Id(doc.Id), ct);

    public async Task DeleteProductIndexAsync(string productId, CancellationToken ct = default)
        => await _client.DeleteAsync(_indexName, productId, ct);
}
```

- [ ] **Step 5: Implement HybridProductSearchService**

`src/Vendor.Infrastructure/Search/HybridProductSearchService.cs`:
```csharp
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Application.Modules.Customers.Queries;

namespace Vendor.Infrastructure.Search;

public class HybridProductSearchService : IProductSearchService
{
    private readonly IProductSearchService _efCoreService;
    private readonly IProductSearchService? _elasticsearchService;

    public HybridProductSearchService(
        EfCoreProductSearchService efCoreService,
        IProductSearchService? elasticsearchService = null)
    {
        _efCoreService = efCoreService ?? throw new ArgumentNullException(nameof(efCoreService));
        _elasticsearchService = elasticsearchService;
    }

    private IProductSearchService Active => _elasticsearchService ?? _efCoreService;

    public Task<PagedResult<ProductSearchDoc>> SearchProductsAsync(
        string? query, ProductSearchFilters filters, int page, int pageSize, CancellationToken ct = default)
        => Active.SearchProductsAsync(query, filters, page, pageSize, ct);

    public Task IndexProductAsync(ProductSearchDoc doc, CancellationToken ct = default)
        => Active.IndexProductAsync(doc, ct);

    public Task DeleteProductIndexAsync(string productId, CancellationToken ct = default)
        => Active.DeleteProductIndexAsync(productId, ct);
}
```

- [ ] **Step 6: Register in DependencyInjection.cs**

Add usings:
```csharp
using Elastic.Clients.Elasticsearch;
using Vendor.Infrastructure.Search;
```

Add in `AddInfrastructure`, before the JWT block:
```csharp
// Search: Elasticsearch when configured, EF Core fallback
services.AddScoped<EfCoreProductSearchService>();

var esUri = configuration["Elasticsearch:Uri"];
if (!string.IsNullOrWhiteSpace(esUri))
{
    services.AddSingleton<ElasticsearchClient>(_ =>
        new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(esUri))));
    var esIndex = configuration["Elasticsearch:IndexName"] ?? "products";
    services.AddScoped<ElasticsearchProductSearchService>(sp =>
        new ElasticsearchProductSearchService(sp.GetRequiredService<ElasticsearchClient>(), esIndex));
    services.AddScoped<IProductSearchService>(sp =>
        new HybridProductSearchService(
            sp.GetRequiredService<EfCoreProductSearchService>(),
            sp.GetRequiredService<ElasticsearchProductSearchService>()));
}
else
{
    services.AddScoped<IProductSearchService>(sp =>
        new HybridProductSearchService(sp.GetRequiredService<EfCoreProductSearchService>(), null));
}
```

- [ ] **Step 7: Run full suite**

```
dotnet test Vendor.slnx
```

Expected: all tests pass.

- [ ] **Step 8: Commit**

```
git add src/Vendor.Infrastructure/Search/ src/Vendor.Infrastructure/DependencyInjection.cs src/Vendor.Infrastructure/Vendor.Infrastructure.csproj tests/Vendor.Infrastructure.Tests/Search/
git commit -m "feat(search): add Elasticsearch adapter, hybrid selector, and DI wiring"
```

---

## Task 4: ProductIndexSyncJob + ProductIndexedEventHandler

**Files:**
- Create: `src/Vendor.Infrastructure/Search/ProductIndexSyncJob.cs`
- Create: `src/Vendor.Application/Modules/Products/ProductIndexedEventHandler.cs`
- Modify: `src/Vendor.Api/Program.cs` (schedule Hangfire recurring job after UseHangfireDashboard)
- Test: `tests/Vendor.Infrastructure.Tests/Search/ProductIndexSyncJobTests.cs`

**Interfaces:**
- Consumes: `IProductSearchService` (Task 1), `VendorDbContext.Products`, `ProductSearchDoc`, `ProductActivatedEvent` (existing: `record ProductActivatedEvent(ProductId ProductId, string Name, Money BasePrice) : DomainEvent`), `IProductRepository.GetByIdAsync(ProductId, CancellationToken) -> Task<Product?>` (existing), Hangfire `RecurringJob`
- Produces: `ProductIndexSyncJob`, `ProductIndexedEventHandler : INotificationHandler<ProductActivatedEvent>`

Note: `ProductActivatedEvent` does not include `Slug`, `Description`, or `Status` — the `ProductIndexedEventHandler` must reload the full product via `IProductRepository` to build a complete `ProductSearchDoc`.

- [ ] **Step 1: Write failing test**

`tests/Vendor.Infrastructure.Tests/Search/ProductIndexSyncJobTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Infrastructure.Persistence;
using Vendor.Infrastructure.Search;
using Xunit;

namespace Vendor.Infrastructure.Tests.Search;

public class ProductIndexSyncJobTests
{
    private VendorDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new VendorDbContext(options);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyDb_NeverCallsIndexProductAsync()
    {
        await using var ctx = CreateInMemoryContext();
        var mockSearch = new Mock<IProductSearchService>();
        var job = new ProductIndexSyncJob(ctx, mockSearch.Object);

        await job.ExecuteAsync(CancellationToken.None);

        mockSearch.Verify(
            s => s.IndexProductAsync(It.IsAny<ProductSearchDoc>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
```

- [ ] **Step 2: Run to confirm FAIL**

```
dotnet test tests/Vendor.Infrastructure.Tests/Vendor.Infrastructure.Tests.csproj --filter "ProductIndexSyncJob" -v normal
```

- [ ] **Step 3: Implement ProductIndexSyncJob**

`src/Vendor.Infrastructure/Search/ProductIndexSyncJob.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Domain.Aggregates.Product;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Search;

public class ProductIndexSyncJob(VendorDbContext dbContext, IProductSearchService searchService)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active)
            .ToListAsync(ct);

        foreach (var product in products)
        {
            var doc = new ProductSearchDoc(
                product.Id.Value.ToString(),
                product.Name,
                product.Slug.Value,
                product.Description,
                product.BasePrice.Amount,
                product.BasePrice.Currency,
                product.Status.ToString(),
                product.CreatedAtUtc);

            await searchService.IndexProductAsync(doc, ct);
        }
    }
}
```

- [ ] **Step 4: Implement ProductIndexedEventHandler**

`src/Vendor.Application/Modules/Products/ProductIndexedEventHandler.cs`:
```csharp
using MediatR;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Domain.Events;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Products;

public class ProductIndexedEventHandler(
    IProductSearchService searchService,
    IProductRepository productRepository) : INotificationHandler<ProductActivatedEvent>
{
    public async Task Handle(ProductActivatedEvent notification, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(notification.ProductId, cancellationToken);
        if (product is null) return;

        var doc = new ProductSearchDoc(
            product.Id.Value.ToString(),
            product.Name,
            product.Slug.Value,
            product.Description,
            product.BasePrice.Amount,
            product.BasePrice.Currency,
            product.Status.ToString(),
            product.CreatedAtUtc);

        await searchService.IndexProductAsync(doc, cancellationToken);
    }
}
```

- [ ] **Step 5: Register ProductIndexSyncJob + schedule recurring job**

Add to `src/Vendor.Infrastructure/DependencyInjection.cs`:
```csharp
services.AddScoped<ProductIndexSyncJob>();
```

Add to `src/Vendor.Api/Program.cs` after `app.UseHangfireDashboard(...)`:
```csharp
RecurringJob.AddOrUpdate<ProductIndexSyncJob>(
    "product-index-sync",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/5 * * * *");
```

Also add using at top of Program.cs: `using Hangfire;`

- [ ] **Step 6: Run full suite**

```
dotnet test Vendor.slnx
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```
git add src/Vendor.Infrastructure/Search/ProductIndexSyncJob.cs src/Vendor.Application/Modules/Products/ProductIndexedEventHandler.cs src/Vendor.Api/Program.cs src/Vendor.Infrastructure/DependencyInjection.cs tests/Vendor.Infrastructure.Tests/Search/ProductIndexSyncJobTests.cs
git commit -m "feat(search): add ProductIndexSyncJob (Hangfire every 5 min) and ProductIndexedEventHandler"
```

---

## Task 5: Product Search Endpoint

**Files:**
- Create: `src/Vendor.Api/Endpoints/ProductSearchEndpoints.cs`
- Modify: `src/Vendor.Api/Extensions/WebApplicationExtensions.cs`
- Test: `tests/Vendor.Api.Tests/Integration/ProductSearchEndpointTests.cs`

**Interfaces:**
- Consumes: `IProductSearchService` (Task 1), `ProductSearchFilters`, `PagedResult<ProductSearchDoc>`
- Produces: `GET /api/v1/products/search` — public (no auth)

- [ ] **Step 1: Write failing integration test**

`tests/Vendor.Api.Tests/Integration/ProductSearchEndpointTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Vendor.Api.Tests.Helpers;
using Xunit;

namespace Vendor.Api.Tests.Integration;

public class ProductSearchEndpointTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;
    public ProductSearchEndpointTests(VendorApiFactory factory) { _factory = factory; }

    [Fact]
    public async Task GetSearch_WithNoQuery_ReturnsOkWithPagedResult()
    {
        var client = _factory.CreateClient(); // public endpoint — no auth required
        var response = await client.GetAsync("/api/v1/products/search");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("items");
        body.Should().Contain("totalCount");
    }

    [Fact]
    public async Task GetSearch_PageSizeOver100_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/products/search?pageSize=500");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetSearch_WithFilters_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/products/search?q=shoe&minPrice=10&maxPrice=200&page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 2: Run to confirm FAIL (404 expected)**

```
dotnet test tests/Vendor.Api.Tests/Vendor.Api.Tests.csproj --filter "ProductSearchEndpoint" -v normal
```

- [ ] **Step 3: Implement ProductSearchEndpoints**

`src/Vendor.Api/Endpoints/ProductSearchEndpoints.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;

namespace Vendor.Api.Endpoints;

public static class ProductSearchEndpoints
{
    public static RouteGroupBuilder MapProductSearchEndpoints(this RouteGroupBuilder group)
    {
        var products = group.MapGroup("/products").WithTags("Products");

        products.MapGet("/search", async (
            string? q,
            decimal? minPrice,
            decimal? maxPrice,
            string? status,
            int page,
            int pageSize,
            IProductSearchService searchService,
            CancellationToken ct) =>
        {
            page = page < 1 ? 1 : page;
            if (pageSize < 1 || pageSize > 100)
                return Results.BadRequest(new { Error = "pageSize must be between 1 and 100." });

            var filters = new ProductSearchFilters(minPrice, maxPrice, status ?? "Active");
            var result = await searchService.SearchProductsAsync(q, filters, page, pageSize, ct);
            return Results.Ok(result);
        })
        .WithName("SearchProducts");

        return group;
    }
}
```

- [ ] **Step 4: Register endpoint**

In `src/Vendor.Api/Extensions/WebApplicationExtensions.cs`, add inside the `v1` group (after existing endpoint registrations):
```csharp
v1.MapProductSearchEndpoints();
```

- [ ] **Step 5: Run full suite**

```
dotnet test Vendor.slnx
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```
git add src/Vendor.Api/Endpoints/ProductSearchEndpoints.cs src/Vendor.Api/Extensions/WebApplicationExtensions.cs tests/Vendor.Api.Tests/Integration/ProductSearchEndpointTests.cs
git commit -m "feat(search): add GET /api/v1/products/search public endpoint"
```

---

## Task 6: Live Shippo Adapter + Hybrid Shipping Provider

**Files:**
- Modify: `src/Vendor.Infrastructure/Shipping/ShippoShippingProvider.cs` (replace stub with live HTTP)
- Create: `src/Vendor.Infrastructure/Shipping/HybridShippingProvider.cs`
- Modify: `src/Vendor.Infrastructure/DependencyInjection.cs`
- Test: `tests/Vendor.Infrastructure.Tests/Shipping/ShippoShippingProviderTests.cs`
- Test: `tests/Vendor.Infrastructure.Tests/Shipping/HybridShippingProviderTests.cs`

**Interfaces:**
- Consumes: `IShippingProvider` (existing — see below), `FlatRateShippingProvider` (existing — returns one `ShippingRate("FLAT", "Flat Rate Ground", Money(5.00m,"USD"), TimeSpan.FromDays(3))`)
- `IShippingProvider` signatures:
  - `GetRatesAsync(Address origin, Address destination, Weight weight, Dimensions dimensions, CancellationToken) -> Task<IReadOnlyList<ShippingRate>>`
  - `CreateLabelAsync(ShippingRate selectedRate, Address origin, Address destination, CancellationToken) -> Task<ShippingLabel>`
  - `TrackShipmentAsync(string trackingNumber, string carrierCode, CancellationToken) -> Task<ShipmentTrackingInfo>`
- Value object constructors (verify by reading `src/Vendor.Domain/ValueObjects/` before coding):
  - `Address(string Street, string City, string State, string ZipCode, string Country)`
  - `Weight(decimal Grams)`
  - `Dimensions(decimal LengthCm, decimal WidthCm, decimal HeightCm)`
  - `Money(decimal Amount, string Currency)`
  - `ShippingRate(string ServiceCode, string ServiceName, Money Cost, TimeSpan EstimatedDeliveryTime)`
  - `ShippingLabel(string TrackingNumber, string LabelUrl, string CarrierCode)`
  - `ShipmentTrackingInfo(string TrackingNumber, string Status, string CurrentLocation, DateTime LastUpdatedUtc)`
- Produces: `ShippoShippingProvider(HttpClient httpClient, string apiKey) : IShippingProvider`, `HybridShippingProvider : IShippingProvider`

- [ ] **Step 1: Read value object constructors**

Before writing any code, read `src/Vendor.Domain/ValueObjects/` to verify exact constructor parameters for `Address`, `Weight`, `Dimensions`. Adjust any test construction calls if they differ from what is listed above.

- [ ] **Step 2: Write failing tests**

`tests/Vendor.Infrastructure.Tests/Shipping/ShippoShippingProviderTests.cs`:
```csharp
using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Shipping;
using Xunit;

namespace Vendor.Infrastructure.Tests.Shipping;

public class ShippoShippingProviderTests
{
    private static HttpClient CreateMockedClient(HttpStatusCode status, string json)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.goshippo.com/") };
    }

    [Fact]
    public async Task GetRatesAsync_OnApiSuccess_ReturnsRates()
    {
        var json = """
        {
          "rates": [{
            "servicelevel": { "token": "usps_priority", "name": "USPS Priority Mail" },
            "amount": "7.50", "currency": "USD", "estimated_days": 2, "object_id": "rate-123"
          }]
        }
        """;
        var client = CreateMockedClient(HttpStatusCode.OK, json);
        var svc = new ShippoShippingProvider(client, "test-key");
        var origin = new Address("123 Main St", "New York", "NY", "10001", "US");
        var dest = new Address("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

        var rates = await svc.GetRatesAsync(origin, dest, new Weight(500m), new Dimensions(10m, 10m, 10m));

        rates.Should().HaveCount(1);
        rates[0].ServiceCode.Should().Be("usps_priority");
        rates[0].Cost.Amount.Should().Be(7.50m);
    }

    [Fact]
    public async Task GetRatesAsync_OnApiFailure_ThrowsHttpRequestException()
    {
        var client = CreateMockedClient(HttpStatusCode.Unauthorized, "{}");
        var svc = new ShippoShippingProvider(client, "bad-key");
        var origin = new Address("123 Main St", "New York", "NY", "10001", "US");
        var dest = new Address("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

        var act = async () => await svc.GetRatesAsync(origin, dest, new Weight(500m), new Dimensions(10m, 10m, 10m));

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
```

`tests/Vendor.Infrastructure.Tests/Shipping/HybridShippingProviderTests.cs`:
```csharp
using FluentAssertions;
using Moq;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Shipping;
using Xunit;

namespace Vendor.Infrastructure.Tests.Shipping;

public class HybridShippingProviderTests
{
    private static readonly Address Origin = new("123 Main St", "New York", "NY", "10001", "US");
    private static readonly Address Dest = new("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

    [Fact]
    public async Task WhenShippoNotConfigured_UsesFlatRate()
    {
        var flatRate = new FlatRateShippingProvider();
        var hybrid = new HybridShippingProvider(flatRate, shippoProvider: null);
        var rates = await hybrid.GetRatesAsync(Origin, Dest, new Weight(500m), new Dimensions(10m, 10m, 10m));
        rates.Should().HaveCount(1);
        rates[0].ServiceCode.Should().Be("FLAT");
    }

    [Fact]
    public async Task WhenShippoConfigured_DelegatesToShippo()
    {
        IReadOnlyList<ShippingRate> shippoRates = [new ShippingRate("USPS_P", "USPS Priority", new Money(7.50m, "USD"), TimeSpan.FromDays(2))];
        var mockShippo = new Mock<IShippingProvider>();
        mockShippo.Setup(s => s.GetRatesAsync(It.IsAny<Address>(), It.IsAny<Address>(), It.IsAny<Weight>(), It.IsAny<Dimensions>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(shippoRates);
        var hybrid = new HybridShippingProvider(new FlatRateShippingProvider(), mockShippo.Object);

        var rates = await hybrid.GetRatesAsync(Origin, Dest, new Weight(500m), new Dimensions(10m, 10m, 10m));

        rates[0].ServiceCode.Should().Be("USPS_P");
    }

    [Fact]
    public async Task WhenShippoThrowsHttpException_FallsBackToFlatRate()
    {
        var mockShippo = new Mock<IShippingProvider>();
        mockShippo.Setup(s => s.GetRatesAsync(It.IsAny<Address>(), It.IsAny<Address>(), It.IsAny<Weight>(), It.IsAny<Dimensions>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new HttpRequestException("Network error"));
        var hybrid = new HybridShippingProvider(new FlatRateShippingProvider(), mockShippo.Object);

        var rates = await hybrid.GetRatesAsync(Origin, Dest, new Weight(500m), new Dimensions(10m, 10m, 10m));

        rates[0].ServiceCode.Should().Be("FLAT");
    }
}
```

- [ ] **Step 3: Implement live ShippoShippingProvider**

Replace entire body of `src/Vendor.Infrastructure/Shipping/ShippoShippingProvider.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Shipping;

public class ShippoShippingProvider(HttpClient httpClient, string apiKey) : IShippingProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<ShippingRate>> GetRatesAsync(
        Address origin, Address destination, Weight weight, Dimensions dimensions, CancellationToken ct = default)
    {
        var payload = new
        {
            address_from = new { street1 = origin.Street, city = origin.City, state = origin.State, zip = origin.ZipCode, country = origin.Country },
            address_to = new { street1 = destination.Street, city = destination.City, state = destination.State, zip = destination.ZipCode, country = destination.Country },
            parcels = new[] { new { length = (double)dimensions.LengthCm, width = (double)dimensions.WidthCm, height = (double)dimensions.HeightCm, distance_unit = "cm", weight = (double)(weight.Grams / 1000m), mass_unit = "kg" } },
            async_mode = false
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "shipments")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("ShippoToken", apiKey) },
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var rates = new List<ShippingRate>();

        foreach (var r in doc.RootElement.GetProperty("rates").EnumerateArray())
        {
            var code = r.GetProperty("servicelevel").GetProperty("token").GetString() ?? "";
            var name = r.GetProperty("servicelevel").GetProperty("name").GetString() ?? "";
            decimal.TryParse(r.GetProperty("amount").GetString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount);
            var currency = r.GetProperty("currency").GetString() ?? "USD";
            var days = r.TryGetProperty("estimated_days", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : 5;
            rates.Add(new ShippingRate(code, name, new Money(amount, currency), TimeSpan.FromDays(days)));
        }

        return rates;
    }

    public async Task<ShippingLabel> CreateLabelAsync(
        ShippingRate selectedRate, Address origin, Address destination, CancellationToken ct = default)
    {
        var payload = new { rate = selectedRate.ServiceCode, async_mode = false };
        using var request = new HttpRequestMessage(HttpMethod.Post, "transactions")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("ShippoToken", apiKey) },
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var tracking = doc.RootElement.GetProperty("tracking_number").GetString() ?? "";
        var labelUrl = doc.RootElement.GetProperty("label_url").GetString() ?? "";
        return new ShippingLabel(tracking, labelUrl, "SHIPPO");
    }

    public async Task<ShipmentTrackingInfo> TrackShipmentAsync(
        string trackingNumber, string carrierCode, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"tracks/{carrierCode}/{trackingNumber}")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("ShippoToken", apiKey) }
        };

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.TryGetProperty("tracking_status", out var ts)
            ? ts.GetProperty("status").GetString() ?? "Unknown" : "Unknown";
        var location = doc.RootElement.TryGetProperty("tracking_status", out var ts2)
            && ts2.TryGetProperty("location", out var loc) ? loc.GetString() ?? "" : "";
        return new ShipmentTrackingInfo(trackingNumber, status, location, DateTime.UtcNow);
    }
}
```

- [ ] **Step 4: Implement HybridShippingProvider**

`src/Vendor.Infrastructure/Shipping/HybridShippingProvider.cs`:
```csharp
using Microsoft.Extensions.Logging;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Shipping;

public class HybridShippingProvider(
    FlatRateShippingProvider flatRateProvider,
    IShippingProvider? shippoProvider = null,
    ILogger<HybridShippingProvider>? logger = null) : IShippingProvider
{
    public async Task<IReadOnlyList<ShippingRate>> GetRatesAsync(
        Address origin, Address destination, Weight weight, Dimensions dimensions, CancellationToken ct = default)
    {
        if (shippoProvider is null) return await flatRateProvider.GetRatesAsync(origin, destination, weight, dimensions, ct);
        try { return await shippoProvider.GetRatesAsync(origin, destination, weight, dimensions, ct); }
        catch (HttpRequestException ex)
        {
            logger?.LogWarning(ex, "Shippo GetRates failed; using flat rate fallback.");
            return await flatRateProvider.GetRatesAsync(origin, destination, weight, dimensions, ct);
        }
    }

    public async Task<ShippingLabel> CreateLabelAsync(
        ShippingRate selectedRate, Address origin, Address destination, CancellationToken ct = default)
    {
        if (shippoProvider is null) return await flatRateProvider.CreateLabelAsync(selectedRate, origin, destination, ct);
        try { return await shippoProvider.CreateLabelAsync(selectedRate, origin, destination, ct); }
        catch (HttpRequestException ex)
        {
            logger?.LogWarning(ex, "Shippo CreateLabel failed; using flat rate fallback.");
            return await flatRateProvider.CreateLabelAsync(selectedRate, origin, destination, ct);
        }
    }

    public async Task<ShipmentTrackingInfo> TrackShipmentAsync(
        string trackingNumber, string carrierCode, CancellationToken ct = default)
    {
        if (shippoProvider is null) return await flatRateProvider.TrackShipmentAsync(trackingNumber, carrierCode, ct);
        try { return await shippoProvider.TrackShipmentAsync(trackingNumber, carrierCode, ct); }
        catch (HttpRequestException ex)
        {
            logger?.LogWarning(ex, "Shippo Track failed; using flat rate fallback.");
            return await flatRateProvider.TrackShipmentAsync(trackingNumber, carrierCode, ct);
        }
    }
}
```

- [ ] **Step 5: Register in DependencyInjection.cs**

Add usings: `using Vendor.Infrastructure.Shipping;`

Add in `AddInfrastructure` after the search block:
```csharp
// Shipping
services.AddScoped<FlatRateShippingProvider>();
var shippoApiKey = configuration["Shippo:ApiKey"];
if (!string.IsNullOrWhiteSpace(shippoApiKey))
{
    services.AddHttpClient("ShippoClient", client =>
        client.BaseAddress = new Uri("https://api.goshippo.com/"));
    services.AddScoped<IShippingProvider>(sp =>
        new HybridShippingProvider(
            sp.GetRequiredService<FlatRateShippingProvider>(),
            new ShippoShippingProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("ShippoClient"),
                shippoApiKey),
            sp.GetService<ILogger<HybridShippingProvider>>()));
}
else
{
    services.AddScoped<IShippingProvider>(sp =>
        new HybridShippingProvider(sp.GetRequiredService<FlatRateShippingProvider>()));
}
```

- [ ] **Step 6: Run full suite**

```
dotnet test Vendor.slnx
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```
git add src/Vendor.Infrastructure/Shipping/ src/Vendor.Infrastructure/DependencyInjection.cs tests/Vendor.Infrastructure.Tests/Shipping/
git commit -m "feat(shipping): replace Shippo stub with live HTTP adapter and hybrid fallback"
```

---

## Task 7: Live TaxJar Adapter + Hybrid Tax Calculator

**Files:**
- Create: `src/Vendor.Infrastructure/Tax/TaxJarTaxCalculator.cs`
- Create: `src/Vendor.Infrastructure/Tax/HybridTaxCalculator.cs`
- Modify: `src/Vendor.Infrastructure/DependencyInjection.cs`
- Test: `tests/Vendor.Infrastructure.Tests/Tax/TaxJarTaxCalculatorTests.cs`
- Test: `tests/Vendor.Infrastructure.Tests/Tax/HybridTaxCalculatorTests.cs`

**Interfaces:**
- Consumes: `ITaxCalculator.CalculateTaxAsync(IReadOnlyList<OrderLine> lines, Address shippingAddress, string currencyCode, CancellationToken) -> Task<Money>`, `FlatTaxCalculator` (existing — returns 8.875% of subtotal), `OrderLine` (has `Sku`, `Quantity`, `UnitPrice.Amount`, `LineTotal.Amount`)
- Produces: `TaxJarTaxCalculator(HttpClient httpClient, string apiKey) : ITaxCalculator`, `HybridTaxCalculator : ITaxCalculator`

- [ ] **Step 1: Write failing tests**

`tests/Vendor.Infrastructure.Tests/Tax/TaxJarTaxCalculatorTests.cs`:
```csharp
using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Tax;
using Xunit;

namespace Vendor.Infrastructure.Tests.Tax;

public class TaxJarTaxCalculatorTests
{
    private static HttpClient CreateMockedClient(HttpStatusCode status, string json)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.taxjar.com/v2/") };
    }

    private static OrderLine MakeLine(decimal unitPrice, int qty) =>
        new(new OrderId(Guid.NewGuid()), new ProductVariantId(Guid.NewGuid()),
            "Test Product", "SKU-001", qty, new Money(unitPrice, "USD"));

    [Fact]
    public async Task CalculateTaxAsync_OnSuccess_ReturnsTaxAmount()
    {
        var json = """{"tax": {"amount_to_collect": 8.88}}""";
        var client = CreateMockedClient(HttpStatusCode.OK, json);
        var svc = new TaxJarTaxCalculator(client, "test-key");
        var lines = new List<OrderLine> { MakeLine(100m, 1) };
        var address = new Address("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

        var tax = await svc.CalculateTaxAsync(lines, address, "USD");

        tax.Amount.Should().Be(8.88m);
        tax.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task CalculateTaxAsync_OnApiFailure_ThrowsHttpRequestException()
    {
        var client = CreateMockedClient(HttpStatusCode.Unauthorized, "{}");
        var svc = new TaxJarTaxCalculator(client, "bad-key");
        var lines = new List<OrderLine> { MakeLine(100m, 1) };
        var address = new Address("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

        var act = async () => await svc.CalculateTaxAsync(lines, address, "USD");
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
```

`tests/Vendor.Infrastructure.Tests/Tax/HybridTaxCalculatorTests.cs`:
```csharp
using FluentAssertions;
using Moq;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Tax;
using Xunit;

namespace Vendor.Infrastructure.Tests.Tax;

public class HybridTaxCalculatorTests
{
    private static readonly Address ShipAddr = new("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

    private static OrderLine MakeLine(decimal unitPrice, int qty) =>
        new(new OrderId(Guid.NewGuid()), new ProductVariantId(Guid.NewGuid()),
            "Test Product", "SKU-001", qty, new Money(unitPrice, "USD"));

    [Fact]
    public async Task WhenTaxJarNotConfigured_UsesFlatRate()
    {
        var hybrid = new HybridTaxCalculator(new FlatTaxCalculator(), taxJarCalculator: null);
        var lines = new List<OrderLine> { MakeLine(100m, 1) };
        var tax = await hybrid.CalculateTaxAsync(lines, ShipAddr, "USD");
        tax.Amount.Should().Be(Math.Round(100m * 0.08875m, 2));
    }

    [Fact]
    public async Task WhenTaxJarConfigured_DelegatesToIt()
    {
        var mockTj = new Mock<ITaxCalculator>();
        mockTj.Setup(s => s.CalculateTaxAsync(It.IsAny<IReadOnlyList<OrderLine>>(), It.IsAny<Address>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new Money(9.99m, "USD"));
        var hybrid = new HybridTaxCalculator(new FlatTaxCalculator(), mockTj.Object);
        var lines = new List<OrderLine> { MakeLine(100m, 1) };

        var tax = await hybrid.CalculateTaxAsync(lines, ShipAddr, "USD");
        tax.Amount.Should().Be(9.99m);
    }

    [Fact]
    public async Task WhenTaxJarThrows_FallsBackToFlatRate()
    {
        var mockTj = new Mock<ITaxCalculator>();
        mockTj.Setup(s => s.CalculateTaxAsync(It.IsAny<IReadOnlyList<OrderLine>>(), It.IsAny<Address>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new HttpRequestException("TaxJar offline"));
        var hybrid = new HybridTaxCalculator(new FlatTaxCalculator(), mockTj.Object);
        var lines = new List<OrderLine> { MakeLine(100m, 1) };

        var tax = await hybrid.CalculateTaxAsync(lines, ShipAddr, "USD");
        tax.Amount.Should().Be(Math.Round(100m * 0.08875m, 2));
    }
}
```

- [ ] **Step 2: Run to confirm FAIL**

```
dotnet test tests/Vendor.Infrastructure.Tests/Vendor.Infrastructure.Tests.csproj --filter "TaxJarTaxCalculator|HybridTaxCalculator" -v normal
```

- [ ] **Step 3: Implement TaxJarTaxCalculator**

`src/Vendor.Infrastructure/Tax/TaxJarTaxCalculator.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Tax;

public class TaxJarTaxCalculator(HttpClient httpClient, string apiKey) : ITaxCalculator
{
    public async Task<Money> CalculateTaxAsync(
        IReadOnlyList<OrderLine> lines, Address shippingAddress, string currencyCode, CancellationToken ct = default)
    {
        var subtotal = lines.Sum(l => l.LineTotal.Amount);

        var payload = new
        {
            to_zip = shippingAddress.ZipCode,
            to_state = shippingAddress.State,
            to_country = shippingAddress.Country,
            amount = (double)subtotal,
            shipping = 0,
            line_items = lines.Select(l => new
            {
                id = l.Sku,
                quantity = l.Quantity,
                unit_price = (double)l.UnitPrice.Amount
            }).ToArray()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "taxes")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey) },
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var taxAmount = doc.RootElement.GetProperty("tax").GetProperty("amount_to_collect").GetDecimal();
        return new Money(taxAmount, currencyCode);
    }
}
```

- [ ] **Step 4: Implement HybridTaxCalculator**

`src/Vendor.Infrastructure/Tax/HybridTaxCalculator.cs`:
```csharp
using Microsoft.Extensions.Logging;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Tax;

public class HybridTaxCalculator(
    FlatTaxCalculator flatTaxCalculator,
    ITaxCalculator? taxJarCalculator = null,
    ILogger<HybridTaxCalculator>? logger = null) : ITaxCalculator
{
    public async Task<Money> CalculateTaxAsync(
        IReadOnlyList<OrderLine> lines, Address shippingAddress, string currencyCode, CancellationToken ct = default)
    {
        if (taxJarCalculator is null)
            return await flatTaxCalculator.CalculateTaxAsync(lines, shippingAddress, currencyCode, ct);
        try
        {
            return await taxJarCalculator.CalculateTaxAsync(lines, shippingAddress, currencyCode, ct);
        }
        catch (HttpRequestException ex)
        {
            logger?.LogWarning(ex, "TaxJar failed; falling back to flat rate.");
            return await flatTaxCalculator.CalculateTaxAsync(lines, shippingAddress, currencyCode, ct);
        }
    }
}
```

- [ ] **Step 5: Register in DependencyInjection.cs**

Add using: `using Vendor.Infrastructure.Tax;`

Add in `AddInfrastructure` after the Shippo block:
```csharp
// Tax
services.AddScoped<FlatTaxCalculator>();
var taxJarApiKey = configuration["TaxJar:ApiKey"];
if (!string.IsNullOrWhiteSpace(taxJarApiKey))
{
    services.AddHttpClient("TaxJarClient", client =>
        client.BaseAddress = new Uri("https://api.taxjar.com/v2/"));
    services.AddScoped<ITaxCalculator>(sp =>
        new HybridTaxCalculator(
            sp.GetRequiredService<FlatTaxCalculator>(),
            new TaxJarTaxCalculator(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("TaxJarClient"),
                taxJarApiKey),
            sp.GetService<ILogger<HybridTaxCalculator>>()));
}
else
{
    services.AddScoped<ITaxCalculator>(sp =>
        new HybridTaxCalculator(sp.GetRequiredService<FlatTaxCalculator>()));
}
```

- [ ] **Step 6: Run full suite**

```
dotnet test Vendor.slnx
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```
git add src/Vendor.Infrastructure/Tax/ src/Vendor.Infrastructure/DependencyInjection.cs tests/Vendor.Infrastructure.Tests/Tax/
git commit -m "feat(tax): add TaxJar live adapter and HybridTaxCalculator with flat rate fallback"
```

---

## Task 8: Shipping Rates Endpoint + Final Verification

**Files:**
- Create: `src/Vendor.Api/Endpoints/ShipmentRatesEndpoints.cs`
- Modify: `src/Vendor.Api/Extensions/WebApplicationExtensions.cs`
- Test: `tests/Vendor.Api.Tests/Integration/ShipmentRatesEndpointTests.cs`

**Interfaces:**
- Consumes: `IShippingProvider`, `ShippingRate`, `Address`, `Weight`, `Dimensions`
- Produces: `GET /api/v1/shipments/rates` — requires authentication

- [ ] **Step 1: Write failing integration test**

`tests/Vendor.Api.Tests/Integration/ShipmentRatesEndpointTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Vendor.Api.Tests.Helpers;
using Xunit;

namespace Vendor.Api.Tests.Integration;

public class ShipmentRatesEndpointTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;
    public ShipmentRatesEndpointTests(VendorApiFactory factory) { _factory = factory; }

    [Fact]
    public async Task GetRates_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/shipments/rates?originZip=10001&destinationZip=90001&weightGrams=500");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRates_WithAuth_ReturnsOkWithRates()
    {
        var client = _factory.CreateClient().WithCustomerBearerToken();
        var response = await client.GetAsync("/api/v1/shipments/rates?originZip=10001&destinationZip=90001&weightGrams=500");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("serviceCode");
    }

    [Fact]
    public async Task GetRates_MissingOriginZip_ReturnsBadRequest()
    {
        var client = _factory.CreateClient().WithCustomerBearerToken();
        var response = await client.GetAsync("/api/v1/shipments/rates?destinationZip=90001&weightGrams=500");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 2: Run to confirm FAIL (404 expected)**

```
dotnet test tests/Vendor.Api.Tests/Vendor.Api.Tests.csproj --filter "ShipmentRatesEndpoint" -v normal
```

- [ ] **Step 3: Implement ShipmentRatesEndpoints**

`src/Vendor.Api/Endpoints/ShipmentRatesEndpoints.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Api.Endpoints;

public static class ShipmentRatesEndpoints
{
    public static RouteGroupBuilder MapShipmentRatesEndpoints(this RouteGroupBuilder group)
    {
        var shipments = group.MapGroup("/shipments").WithTags("Shipments");

        shipments.MapGet("/rates", async (
            string? originZip,
            string? destinationZip,
            int? weightGrams,
            IShippingProvider shippingProvider,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(originZip))
                return Results.BadRequest(new { Error = "originZip is required." });
            if (string.IsNullOrWhiteSpace(destinationZip))
                return Results.BadRequest(new { Error = "destinationZip is required." });
            if (!weightGrams.HasValue || weightGrams.Value <= 0)
                return Results.BadRequest(new { Error = "weightGrams must be a positive integer." });

            var origin = new Address("N/A", "N/A", "N/A", originZip, "US");
            var dest = new Address("N/A", "N/A", "N/A", destinationZip, "US");
            var weight = new Weight(weightGrams.Value);
            var dimensions = new Dimensions(10m, 10m, 10m);

            var rates = await shippingProvider.GetRatesAsync(origin, dest, weight, dimensions, ct);

            var dtos = rates.Select(r => new
            {
                serviceCode = r.ServiceCode,
                serviceName = r.ServiceName,
                amount = r.Cost.Amount,
                currency = r.Cost.Currency,
                estimatedDays = (int)r.EstimatedDeliveryTime.TotalDays
            });

            return Results.Ok(dtos);
        }).RequireAuthorization();

        return group;
    }
}
```

- [ ] **Step 4: Register endpoint**

In `src/Vendor.Api/Extensions/WebApplicationExtensions.cs`, add:
```csharp
v1.MapShipmentRatesEndpoints();
```

- [ ] **Step 5: Run full suite**

```
dotnet test Vendor.slnx
```

Expected: all tests pass.

- [ ] **Step 6: Final commit**

```
git add src/Vendor.Api/Endpoints/ShipmentRatesEndpoints.cs src/Vendor.Api/Extensions/WebApplicationExtensions.cs tests/Vendor.Api.Tests/Integration/ShipmentRatesEndpointTests.cs
git commit -m "feat(shipping): add GET /api/v1/shipments/rates authenticated endpoint"
```

- [ ] **Step 7: Update Graphify knowledge graph**

```
graphify update .
```

---

## Spec Coverage Self-Review

| Spec Requirement | Task |
|-----------------|------|
| `IProductSearchService` interface in Application layer | Task 1 |
| `ProductSearchDoc` + `ProductSearchFilters` models | Task 1 |
| `EfCoreProductSearchService` fallback adapter | Task 2 |
| `ElasticsearchProductSearchService` (Elastic.Clients.Elasticsearch 8.x) | Task 3 |
| `HybridProductSearchService` — ES when `Elasticsearch:Uri` set, EF Core otherwise | Task 3 |
| `ProductIndexSyncJob` Hangfire recurring every 5 min | Task 4 |
| `ProductIndexedEventHandler` on `ProductActivatedEvent` | Task 4 |
| `GET /api/v1/products/search` public endpoint, pageSize guard, filters | Task 5 |
| Live `ShippoShippingProvider` HTTP adapter with JSON parsing | Task 6 |
| `HybridShippingProvider` — Shippo when `Shippo:ApiKey` set, flat rate fallback | Task 6 |
| `TaxJarTaxCalculator` HTTP adapter parsing `tax.amount_to_collect` | Task 7 |
| `HybridTaxCalculator` — TaxJar when `TaxJar:ApiKey` set, flat rate fallback | Task 7 |
| `GET /api/v1/shipments/rates` authenticated endpoint | Task 8 |
| Config keys `Elasticsearch:Uri`, `Shippo:ApiKey`, `TaxJar:ApiKey` all optional | Tasks 3, 6, 7 |
| Unit tests for all adapters with mocked HTTP / ES client | Tasks 2, 3, 6, 7 |
| Integration tests for search and rates endpoints | Tasks 5, 8 |
