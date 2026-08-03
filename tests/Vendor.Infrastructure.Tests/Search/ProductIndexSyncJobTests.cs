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
