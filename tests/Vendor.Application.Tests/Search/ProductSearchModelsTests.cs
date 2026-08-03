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
