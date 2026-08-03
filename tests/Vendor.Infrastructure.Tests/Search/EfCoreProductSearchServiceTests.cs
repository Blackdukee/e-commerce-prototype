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
