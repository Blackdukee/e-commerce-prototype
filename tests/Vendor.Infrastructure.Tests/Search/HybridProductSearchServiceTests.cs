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
    public async Task WhenElasticsearchNotProvided_UsesEfCoreFallback()
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
        mockEs.Setup(s => s.SearchProductsAsync(
                It.IsAny<string?>(), It.IsAny<ProductSearchFilters>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new PagedResult<ProductSearchDoc>([], 0, 1, 20));

        await using var ctx = CreateInMemoryContext();
        var efService = new EfCoreProductSearchService(ctx);
        var hybrid = new HybridProductSearchService(efService, mockEs.Object);

        await hybrid.SearchProductsAsync(null, new ProductSearchFilters(), 1, 20);

        mockEs.Verify(s => s.SearchProductsAsync(
            null, It.IsAny<ProductSearchFilters>(), 1, 20, default), Times.Once);
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

    [Fact]
    public async Task DeleteProductIndexAsync_WhenEsConfigured_DelegatesToEs()
    {
        var mockEs = new Mock<IProductSearchService>();
        mockEs.Setup(s => s.DeleteProductIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        await using var ctx = CreateInMemoryContext();
        var efService = new EfCoreProductSearchService(ctx);
        var hybrid = new HybridProductSearchService(efService, mockEs.Object);

        await hybrid.DeleteProductIndexAsync("p1");

        mockEs.Verify(s => s.DeleteProductIndexAsync("p1", default), Times.Once);
    }
}
