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
