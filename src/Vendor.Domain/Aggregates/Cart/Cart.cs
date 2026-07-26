using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;

namespace Vendor.Domain.Aggregates.Cart;

public enum CartStatus
{
    Active,
    Merged,
    ConvertedToOrder,
    Abandoned
}

public class Cart : AggregateRoot<CartId>
{
    private readonly List<CartItem> _items = [];

    public CustomerId? CustomerId { get; private set; }
    public string? SessionId { get; private set; }
    public CartStatus Status { get; private set; }
    public string? DiscountCode { get; private set; }
    public DateTime LastModifiedUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    private Cart() : base(default!)
    {
    }

    public Cart(CartId id, CustomerId? customerId = null, string? sessionId = null) : base(id)
    {
        if (customerId == null && string.IsNullOrWhiteSpace(sessionId))
        {
            throw new BusinessRuleViolationException("Cart must belong to either a CustomerId or a SessionId.", nameof(Cart));
        }

        CustomerId = customerId;
        SessionId = sessionId;
        Status = CartStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
        LastModifiedUtc = CreatedAtUtc;
    }

    public void AddItem(CartItem item, int maxItemsPerOrder = 50)
    {
        ArgumentNullException.ThrowIfNull(item, nameof(item));
        EnsureActive();

        var existingItem = _items.FirstOrDefault(i => i.ProductVariantId == item.ProductVariantId);

        if (existingItem == null)
        {
            if (_items.Count >= maxItemsPerOrder)
            {
                throw new BusinessRuleViolationException(
                    $"Cart item limit exceeded. Maximum items allowed per order is {maxItemsPerOrder}.",
                    nameof(Cart));
            }

            _items.Add(item);
        }
        else
        {
            existingItem.UpdateQuantity(existingItem.Quantity + item.Quantity);
        }

        Touch();
    }

    public void RemoveItem(ProductVariantId variantId)
    {
        EnsureActive();
        var removed = _items.RemoveAll(i => i.ProductVariantId == variantId);
        if (removed > 0)
        {
            Touch();
        }
    }

    public void ApplyDiscount(string discountCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discountCode, nameof(discountCode));
        EnsureActive();

        DiscountCode = discountCode.Trim().ToUpperInvariant();
        Touch();
    }

    public void RemoveDiscount()
    {
        EnsureActive();
        DiscountCode = null;
        Touch();
    }

    public void Merge(Cart guestCart)
    {
        ArgumentNullException.ThrowIfNull(guestCart, nameof(guestCart));
        EnsureActive();

        if (CustomerId == null)
        {
            throw new BusinessRuleViolationException("Target cart must belong to a registered customer to accept merge.", nameof(Cart));
        }

        if (guestCart.Status != CartStatus.Active)
        {
            throw new BusinessRuleViolationException("Source guest cart must be Active to be merged.", nameof(Cart));
        }

        foreach (var guestItem in guestCart.Items)
        {
            AddItem(new CartItem(Id, guestItem.ProductVariantId, guestItem.Quantity, guestItem.UnitPrice));
        }

        guestCart.Status = CartStatus.Merged;
        guestCart.Touch();
        Touch();
    }

    public bool IsAbandoned(DateTime utcNow, TimeSpan abandonmentTimeout)
    {
        return Status == CartStatus.Active && (utcNow - LastModifiedUtc) >= abandonmentTimeout;
    }

    public void MarkAbandoned(DateTime utcNow, TimeSpan abandonmentTimeout)
    {
        if (!IsAbandoned(utcNow, abandonmentTimeout))
        {
            throw new BusinessRuleViolationException("Cart does not meet abandonment criteria.", nameof(Cart));
        }

        Status = CartStatus.Abandoned;
        Touch();

        RaiseDomainEvent(new CartAbandonedEvent(Id, CustomerId, LastModifiedUtc));
    }

    public void MarkConvertedToOrder()
    {
        EnsureActive();
        Status = CartStatus.ConvertedToOrder;
        Touch();
    }

    private void EnsureActive()
    {
        if (Status != CartStatus.Active)
        {
            throw new BusinessRuleViolationException($"Cannot perform operation on cart in '{Status}' status.", nameof(Cart));
        }
    }

    private void Touch()
    {
        LastModifiedUtc = DateTime.UtcNow;
    }
}
