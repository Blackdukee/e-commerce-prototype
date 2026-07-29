using FluentAssertions;
using Vendor.Domain.Aggregates.Payment;

namespace Vendor.Domain.Tests.Payments;

public class WebhookEventEntryTests
{
    [Fact]
    public void WebhookEventEntry_Initialization_SetsPropertiesCorrectly()
    {
        var entry = new WebhookEventEntry("Stripe", "evt_12345", "payment_intent.succeeded", "hash_987");

        entry.GatewayName.Should().Be("Stripe");
        entry.EventId.Should().Be("evt_12345");
        entry.EventType.Should().Be("payment_intent.succeeded");
        entry.PayloadHash.Should().Be("hash_987");
        entry.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public void WebhookEventEntry_MarkProcessed_UpdatesState()
    {
        var entry = new WebhookEventEntry("Stripe", "evt_12345", "payment_intent.succeeded", "hash_987");

        entry.MarkProcessed();

        entry.IsProcessed.Should().BeTrue();
    }
}
