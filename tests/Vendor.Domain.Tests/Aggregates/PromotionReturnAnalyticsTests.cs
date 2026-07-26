using FluentAssertions;
using Vendor.Domain.Aggregates.AnalyticsEvent;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.Promotion;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.Aggregates;

public class PromotionReturnAnalyticsTests
{
    [Fact]
    public void Promotion_UsageLimitExhaustion_AutoDeactivatesAndRaisesEvent()
    {
        var validity = new DateRange(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var promo = new Promotion(PromotionId.New(), "PROMO10", DiscountType.Percentage, 10m, validity, maxUsageCount: 2);

        promo.CalculateDiscount(new Money(100m, "USD"), DateTime.UtcNow);
        promo.RecordUsage();
        promo.IsActive.Should().BeTrue();

        promo.CalculateDiscount(new Money(100m, "USD"), DateTime.UtcNow);
        promo.RecordUsage(); // 2nd usage hit max
        promo.IsActive.Should().BeFalse();
        promo.DomainEvents.Should().ContainSingle(e => e is PromotionExhaustedEvent);

        Action act = () => promo.CalculateDiscount(new Money(100m, "USD"), DateTime.UtcNow);
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public void ReturnRequest_RefundVsExchangeDivergence_Succeeds()
    {
        var orderId = OrderId.New();
        var customerId = CustomerId.New();
        var item = new ReturnItem(Guid.NewGuid(), ProductVariantId.New(), 1, "Defective item");

        // Refund path
        var refundReq = new ReturnRequest(ReturnRequestId.New(), orderId, customerId, "Defective", [item]);
        refundReq.Approve(ResolutionType.Refund);
        refundReq.CompleteReturn();
        refundReq.Status.Should().Be(ReturnRequestStatus.Returned);
        refundReq.DomainEvents.Should().Contain(e => e is ReturnCompletedEvent);

        // Exchange path
        var exchangeReq = new ReturnRequest(ReturnRequestId.New(), orderId, customerId, "Size too small", [item]);
        exchangeReq.Approve(ResolutionType.Exchange);
        exchangeReq.CompleteExchange(OrderId.New());
        exchangeReq.Status.Should().Be(ReturnRequestStatus.Exchanged);
        exchangeReq.DomainEvents.Should().Contain(e => e is ExchangeCompletedEvent);
    }

    [Fact]
    public void ReturnRequest_EmptyItems_ThrowsBusinessRuleViolationException()
    {
        Action act = () => _ = new ReturnRequest(ReturnRequestId.New(), OrderId.New(), CustomerId.New(), "No reason", []);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*at least one item*");
    }

    [Fact]
    public void AnalyticsEvent_Capture_StoresImmutableConsentSnapshot()
    {
        var customerId = CustomerId.New();
        var analyticsEvent = AnalyticsEvent.Capture(customerId, "ProductViewed", "{\"sku\":\"SKU1\"}", consentGrantedAtCapture: true);

        analyticsEvent.CustomerId.Should().Be(customerId);
        analyticsEvent.EventType.Should().Be("ProductViewed");
        analyticsEvent.ConsentGrantedAtCapture.Should().BeTrue();
        analyticsEvent.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
