using FluentAssertions;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.Aggregates;

public class CustomerAndCartTests
{
    [Fact]
    public void Customer_ConvertToRegistered_UpdatesTypeAndRaisesEvent()
    {
        var customer = new Customer(CustomerId.New(), "guest@example.com", "John", "Doe");

        customer.ConvertToRegistered("john.doe@example.com");

        customer.CustomerType.Should().Be(CustomerType.Registered);
        customer.Email.Should().Be("john.doe@example.com");
        customer.DomainEvents.Should().ContainSingle(e => e is CustomerCreatedEvent);
    }

    [Fact]
    public void Customer_UpdateConsent_UpdatesStateAndRaisesEvent()
    {
        var customer = new Customer(CustomerId.New(), "user@example.com", "Jane", "Doe");

        customer.UpdateConsent(true);

        customer.AnalyticsConsent.Should().BeTrue();
        customer.DomainEvents.Should().ContainSingle(e => e is CustomerConsentUpdatedEvent);
    }

    [Fact]
    public void Cart_ExceedMaxItems_ThrowsException()
    {
        var cart = new Cart(CartId.New(), CustomerId.New());
        var price = new Money(10m, "USD");

        cart.AddItem(new CartItem(cart.Id, ProductVariantId.New(), 1, price), maxItemsPerOrder: 2);
        cart.AddItem(new CartItem(cart.Id, ProductVariantId.New(), 1, price), maxItemsPerOrder: 2);

        Action act = () => cart.AddItem(new CartItem(cart.Id, ProductVariantId.New(), 1, price), maxItemsPerOrder: 2);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*limit exceeded*");
    }

    [Fact]
    public void Cart_ApplyDiscount_ReplacesPreviousCode()
    {
        var cart = new Cart(CartId.New(), CustomerId.New());

        cart.ApplyDiscount("CODE1");
        cart.DiscountCode.Should().Be("CODE1");

        cart.ApplyDiscount("CODE2");
        cart.DiscountCode.Should().Be("CODE2");
    }

    [Fact]
    public void Cart_MergeGuestCart_CopiesItemsAndMarksGuestMerged()
    {
        var customerId = CustomerId.New();
        var customerCart = new Cart(CartId.New(), customerId);
        var guestCart = new Cart(CartId.New(), sessionId: "sess-123");

        var variantId = ProductVariantId.New();
        guestCart.AddItem(new CartItem(guestCart.Id, variantId, 2, new Money(15m, "USD")));

        customerCart.Merge(guestCart);

        customerCart.Items.Should().ContainSingle(i => i.ProductVariantId == variantId && i.Quantity == 2);
        guestCart.Status.Should().Be(CartStatus.Merged);
    }

    [Fact]
    public void Cart_MarkAbandoned_TransitionsStatusAndRaisesEvent()
    {
        var cart = new Cart(CartId.New(), CustomerId.New());
        var now = DateTime.UtcNow;
        var pastTime = now.AddHours(2); // 2 hours later

        cart.IsAbandoned(pastTime, TimeSpan.FromHours(1)).Should().BeTrue();

        cart.MarkAbandoned(pastTime, TimeSpan.FromHours(1));

        cart.Status.Should().Be(CartStatus.Abandoned);
        cart.DomainEvents.Should().ContainSingle(e => e is CartAbandonedEvent);
    }
}
